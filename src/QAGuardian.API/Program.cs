using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QAGuardian.API.Health;
using QAGuardian.API.Hubs;
using QAGuardian.API.Middleware;
using QAGuardian.API.Services;
using QAGuardian.Application;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Infrastructure;
using QAGuardian.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging estructurado (Serilog) ───────────────────────────────────
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ── Capas de la aplicación ───────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireServer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IRunProgressNotifier, SignalRRunProgressNotifier>();

// ── Compresión de respuestas (Brotli/Gzip) ───────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ── Autenticación: JWT local + OIDC externo (OAuth2/OpenID Connect) ─────
// Con Oidc:Authority configurada, los tokens de un proveedor de identidad
// conforme (Entra ID, Google, Keycloak…) se validan por discovery. El esquema
// se selecciona por el emisor del token; el usuario debe existir en QA Guardian
// (aprovisionado) y sus roles RBAC se toman de la base local.
var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey es obligatoria.");
var localIssuer = builder.Configuration["Jwt:Issuer"] ?? "QAGuardian";
var oidcAuthority = builder.Configuration["Oidc:Authority"];
var oidcEnabled = !string.IsNullOrWhiteSpace(oidcAuthority);
const string OidcScheme = "Oidc";
const string MultiAuthScheme = "MultiAuth";
var allowHubQueryToken = builder.Environment.IsDevelopment();

// Token: Authorization (preferido). Query access_token solo en Development (SignalR legado).
string? ExtractToken(HttpContext context)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(header)) return header;

    if (!allowHubQueryToken || !context.Request.Path.StartsWithSegments("/hubs"))
        return null;

    var queryToken = context.Request.Query["access_token"].ToString();
    return string.IsNullOrEmpty(queryToken) ? null : queryToken;
}

void ConfigureHubTokenFromRequest(MessageReceivedContext context, Microsoft.Extensions.Logging.ILogger logger)
{
    // Negotiate / LongPolling: Authorization Bearer.
    var header = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(header)
        && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Token = header["Bearer ".Length..].Trim();
        return;
    }

    if (!context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        return;

    var queryToken = context.Request.Query["access_token"].ToString();
    if (string.IsNullOrEmpty(queryToken))
        return;

    if (allowHubQueryToken)
    {
        logger.LogWarning(
            "SignalR JWT vía query access_token (solo Development). Use Authorization header o LongPolling.");
        context.Token = queryToken;
        return;
    }

    // Production/Staging: rechazar token en query (TM-02).
    logger.LogWarning(
        "SignalR: access_token en query rechazado fuera de Development. Preferir Authorization / LongPolling.");
}

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = MultiAuthScheme;
    options.DefaultChallengeScheme = MultiAuthScheme;
});

authBuilder.AddPolicyScheme(MultiAuthScheme, "JWT local u OIDC externo", options =>
{
    options.ForwardDefaultSelector = context =>
        oidcEnabled && QAGuardian.Infrastructure.Identity.OidcTokenInspector
            .IsExternalToken(ExtractToken(context), localIssuer)
            ? OidcScheme
            : JwtBearerDefaults.AuthenticationScheme;
});

authBuilder.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = localIssuer,
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "QAGuardian.Clients",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    // Hub: Authorization header (preferido). Query access_token solo en Development.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            ConfigureHubTokenFromRequest(
                context,
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("QAGuardian.SignalR.Auth"));
            return Task.CompletedTask;
        }
    };
});

if (oidcEnabled)
{
    authBuilder.AddJwtBearer(OidcScheme, options =>
    {
        options.Authority = oidcAuthority;
        options.Audience = builder.Configuration["Oidc:Audience"];
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Oidc:RequireHttpsMetadata", true);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrEmpty(builder.Configuration["Oidc:Audience"]),
            NameClaimType = "name"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                ConfigureHubTokenFromRequest(
                    context,
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("QAGuardian.SignalR.Auth"));
                return Task.CompletedTask;
            },
            // El proveedor externo autentica la identidad; los roles RBAC son locales.
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var email = principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? principal?.FindFirst("email")?.Value
                            ?? principal?.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                {
                    context.Fail("El token OIDC no contiene un correo electrónico.");
                    return;
                }

                var users = context.HttpContext.RequestServices
                    .GetRequiredService<QAGuardian.Application.Abstractions.Persistence.IUserRepository>();
                var user = await users.GetByEmailAsync(email, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive)
                {
                    context.Fail("El usuario no está aprovisionado en QA Guardian.");
                    return;
                }

                var identity = (System.Security.Claims.ClaimsIdentity)principal!.Identity!;
                identity.AddClaim(new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()));
                identity.AddClaim(new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Email, user.Email));
                foreach (var role in user.Roles)
                    identity.AddClaim(new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.Role, role.Name));
            }
        };
    });
}

// ── Autorización RBAC ────────────────────────────────────────────────
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Administer, p => p.RequireRole(SystemRoles.Administrator))
    .AddPolicy(Policies.ManageProjects, p => p.RequireRole(
        SystemRoles.Administrator, SystemRoles.TechLead, SystemRoles.QA))
    .AddPolicy(Policies.ExecuteTests, p => p.RequireRole(
        SystemRoles.Administrator, SystemRoles.QA, SystemRoles.DevOps, SystemRoles.TechLead))
    .AddPolicy(Policies.ManageDefects, p => p.RequireRole(
        SystemRoles.Administrator, SystemRoles.QA, SystemRoles.Developer, SystemRoles.TechLead))
    .AddPolicy(Policies.ViewReports, p => p.RequireAuthenticatedUser());

// ── Rate limiting (OWASP API4) ───────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 300,
                QueueLimit = 0
            }));
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 10;
        limiter.QueueLimit = 0;
    });
});

// ── CORS ─────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// ── MVC + Versionado + Swagger ───────────────────────────────────────
builder.Services.AddControllers();
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024;
});
// Backplane Redis: obligatorio para progreso correcto con ≥2 réplicas de API.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("qaguardian-signalr");
    });
    Log.Information("SignalR: backplane Redis habilitado (multi-réplica).");
}
else if (!builder.Environment.IsDevelopment())
{
    Log.Warning(
        "ConnectionStrings:Redis vacío: SignalR sin backplane. Con ≥2 réplicas el progreso en tiempo real será inconsistente.");
}
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QA Guardian API",
        Version = "v1",
        Description = "Plataforma empresarial de QA automatizado: quality gate para cualquier proyecto."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Encabezado de autorización JWT. Ejemplo: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health checks (liveness vs readiness) ────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Proceso activo."), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// ── Pipeline HTTP ────────────────────────────────────────────────────
app.UseResponseCompression();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Cabeceras de seguridad (OWASP A05:2025 - Security Misconfiguration).
// CSP en modo report-only-equivalente pragmático para API+Swagger: el HTML servido por esta app
// es solo Swagger UI (Development); la CSP que protege a los usuarios finales del SPA vive en
// nginx.conf, que sirve el HTML real. Se define aquí también por defensa en profundidad y por si
// se accede a Swagger o a alguna vista de error HTML directamente desde la API.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), interest-cohort=()";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " + // Swagger UI inyecta estilos inline
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
    await next();
});

// HSTS (OWASP A02:2025): fuerza HTTPS en el navegador tras la primera visita. Se omite en
// Development para no romper `dotnet run` sobre HTTP en localhost.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "QA Guardian v1"));
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();

app.MapControllers();
app.MapHub<TestRunHub>("/hubs/testruns");
// Liveness: el proceso responde (orquestadores / Docker HEALTHCHECK).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
// Readiness: DB accesible — no enviar tráfico si falla.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()]
});

// ── Inicialización de base de datos ──────────────────────────────────
// OWASP A07:2025 (Identification & Authentication Failures): la contraseña del
// admin semilla NUNCA tiene un valor por defecto embebido en código. Debe
// configurarse explícitamente (env var / user-secrets / secret manager). Fuera
// de Development, además se rechaza si coincide con un valor público conocido
// (el que documentaba el README de versiones anteriores).
//
// Sprint 16-A: en Production/QA/Staging NUNCA se ejecuta Migrate/EnsureCreated
// en el proceso API. El esquema lo aplica un job aparte (dotnet ef database update
// o servicio compose `migrate`). Solo Development puede aplicar migraciones en boot
// (Database:ApplyMigrationsOnStartup=true).
const string KnownPublicDefaultPassword = "QaGuardian.2026!";

if (!app.Configuration.GetValue("Database:SkipInitialization", false))
{
    var seedEmail = app.Configuration["Seed:AdminEmail"];
    var seedPassword = app.Configuration["Seed:AdminPassword"];

    if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        throw new InvalidOperationException(
            "Seed:AdminEmail y Seed:AdminPassword son obligatorios (variables de entorno " +
            "Seed__AdminEmail / Seed__AdminPassword, user-secrets o secret manager). " +
            "No existe un valor por defecto: defina credenciales únicas por ambiente.");

    if (!app.Environment.IsDevelopment()
        && string.Equals(seedPassword, KnownPublicDefaultPassword, StringComparison.Ordinal))
        throw new InvalidOperationException(
            "Seed:AdminPassword coincide con el valor público documentado en versiones anteriores " +
            "de QA Guardian. Defina una contraseña única y secreta para este ambiente.");

    // Hard guard: nunca Migrate/EnsureCreated en el proceso API fuera de Development,
    // aunque alguien force Database__ApplyMigrationsOnStartup=true en prod.
    var applyMigrationsOnStartup =
        app.Environment.IsDevelopment()
        && app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);

    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync(seedEmail, seedPassword, applyMigrationsOnStartup);
}

app.Run();

/// <summary>Políticas de autorización de la plataforma.</summary>
public static class Policies
{
    public const string Administer = "Administer";
    public const string ManageProjects = "ManageProjects";
    public const string ExecuteTests = "ExecuteTests";
    public const string ManageDefects = "ManageDefects";
    public const string ViewReports = "ViewReports";
}

/// <summary>El dashboard de Hangfire solo es visible para administradores autenticados.</summary>
public class HangfireDashboardAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.IsInRole(SystemRoles.Administrator);
    }
}

// Necesario para WebApplicationFactory en pruebas de integración.
public partial class Program { }
