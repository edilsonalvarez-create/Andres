using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

// ── Autenticación JWT ────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey es obligatoria.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "QAGuardian",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "QAGuardian.Clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // Permite el token por query string para el hub de SignalR.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

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
builder.Services.AddSignalR();
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

builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Pipeline HTTP ────────────────────────────────────────────────────
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Cabeceras de seguridad (OWASP A05)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

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
app.MapHealthChecks("/health");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()]
});

// ── Inicialización de base de datos ──────────────────────────────────
if (!app.Configuration.GetValue("Database:SkipInitialization", false))
{
    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync(
        app.Configuration["Seed:AdminEmail"] ?? "admin@qaguardian.local",
        app.Configuration["Seed:AdminPassword"] ?? "QaGuardian.2026!");
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
