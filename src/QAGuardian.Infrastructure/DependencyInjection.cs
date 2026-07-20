using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Infrastructure.Ai;
using QAGuardian.Infrastructure.Caching;
using QAGuardian.Infrastructure.Clients;
using QAGuardian.Infrastructure.Identity;
using QAGuardian.Infrastructure.Jobs;
using QAGuardian.Infrastructure.Notifications;
using QAGuardian.Infrastructure.Persistence;
using QAGuardian.Infrastructure.Reports;
using QAGuardian.Infrastructure.Runners;
using QAGuardian.Infrastructure.Security;
using QAGuardian.Infrastructure.Storage;
using QAGuardian.Infrastructure.Validation;
using QAGuardian.Infrastructure.Visual;

namespace QAGuardian.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Persistencia ─────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useSqlite = configuration.GetValue("Database:UseSqlite", false);
        services.AddDbContext<QAGuardianDbContext>(options =>
        {
            if (useSqlite)
            {
                // Migraciones generadas para SQL Server: el modelo runtime SQLite difiere del
                // snapshot (tipos/anotaciones). Ignorar PendingModelChanges solo en SQLite/dev.
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning));
                options.UseSqlite(connectionString ?? "Data Source=qaguardian.db",
                    sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            else
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.EnableRetryOnFailure(3);
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            if (configuration.GetValue("Database:EnableSensitiveLogging", false))
                options.EnableSensitiveDataLogging();
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITestCaseRepository, TestCaseRepository>();
        services.AddScoped<ITestRunRepository, TestRunRepository>();
        services.AddScoped<IDefectRepository, DefectRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQualityGateRepository, QualityGateRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IProjectDatabaseEnvironmentRepository, ProjectDatabaseEnvironmentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DbInitializer>();

        // ── Identidad y seguridad ────────────────────────────────────
        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IProjectDatabaseConnectionResolver, ProjectDatabaseConnectionResolver>();
        services.AddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
        services.AddSingleton<ISsrfGuard, SsrfGuard>();
        // Sprint 18-A (B2): allowlist de destinos SQL para entornos de BD por proyecto.
        services.Configure<DatabaseValidationOptions>(
            configuration.GetSection(DatabaseValidationOptions.SectionName));
        services.AddSingleton<ISqlHostGuard, SqlHostGuard>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenEncryptionService, AesTokenEncryptionService>();

        // ── Caché ────────────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "qaguardian:";
            });
        else
            services.AddDistributedMemoryCache();
        services.AddScoped<ICacheService, DistributedCacheService>();

        // ── Runners de pruebas ───────────────────────────────────────
        // Registrados como ITestRunner (no por tipo concreto): TestRunnerFactory resuelve
        // inyectando IEnumerable<ITestRunner> e indexando por .Framework — agregar un runner
        // nuevo solo requiere una línea aquí, sin tocar la fábrica (ADR-009).
        services.Configure<RunnerSandboxOptions>(
            configuration.GetSection(RunnerSandboxOptions.SectionName));
        services.AddSingleton<ProcessExecutor>();
        // Sprint 13: workspace + sandbox (local o Docker según Runners:UseSandbox).
        services.AddSingleton<IScriptExecutionEnvironment, LocalScriptExecutionEnvironment>();
        services.AddSingleton<LocalSandboxedProcessExecutor>();
        services.AddSingleton<DockerSandboxedProcessExecutor>();
        // Sprint 13-C: fuera de Development NUNCA fallback host; Dev sin Docker → warning + local.
        services.AddSingleton<ISandboxedProcessExecutor>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RunnerSandboxOptions>>().Value;
            var hostEnv = sp.GetRequiredService<IHostEnvironment>();
            var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("QAGuardian.Runners.Sandbox");

            if (opts.UseSandbox)
            {
                log.LogInformation("Runners:UseSandbox=true → DockerSandboxedProcessExecutor");
                return sp.GetRequiredService<DockerSandboxedProcessExecutor>();
            }

            if (hostEnv.IsDevelopment())
            {
                log.LogWarning(
                    "Runners:UseSandbox=false en Development → fallback LOCAL (host API). " +
                    "No usar este modo fuera de Development.");
                return sp.GetRequiredService<LocalSandboxedProcessExecutor>();
            }

            log.LogWarning(
                "Runners:UseSandbox=false fuera de Development → se fuerza sandbox Docker " +
                "(los scripts de usuario no se ejecutan en el proceso API).");
            return sp.GetRequiredService<DockerSandboxedProcessExecutor>();
        });
        services.AddScoped<ITestRunner, PlaywrightTestRunner>();
        services.AddScoped<ITestRunner, NewmanTestRunner>();
        services.AddScoped<ITestRunner, JMeterTestRunner>();
        services.AddScoped<ITestRunner, ZapScanRunner>();
        services.AddScoped<ITestRunner, VisualRegressionRunner>();
        services.AddScoped<ITestRunner, SeleniumIdeTestRunner>();
        services.AddSingleton<IImageComparer, ImageSharpComparer>();
        services.AddScoped<IPlaywrightRecorder, PlaywrightRecorder>();
        services.AddScoped<ITestRunnerFactory, TestRunnerFactory>();

        // ── Integraciones externas ───────────────────────────────────
        services.AddScoped<IntegrationSettingResolver>();
        services.AddTransient<SsrfOutboundHandler>();
        // SonarQube: BaseUrl configurable por proyecto → handler SSRF en cada request.
        services.AddHttpClient<ISonarQubeClient, SonarQubeClient>()
            .AddHttpMessageHandler<SsrfOutboundHandler>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
        // GitHub API host fijo (api.github.com); BaseUrl del setting es repo, no API.
        services.AddHttpClient<IGitHubClient, GitHubApiClient>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
        // Webhooks Teams/Slack/Discord/Telegram → SsrfGuard en handler + NotificationDispatcher.
        services.AddHttpClient("notifications")
            .AddHttpMessageHandler<SsrfOutboundHandler>()
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IIntegrationConnectionTester, IntegrationConnectionTester>();
        services.AddScoped<IDatabaseSchemaValidator, SqlServerSchemaValidator>();

        // ── Agente IA (Sprint 7: opciones de costo/precisión) ─────────
        services.Configure<AnthropicAiOptions>(configuration.GetSection(AnthropicAiOptions.SectionName));
        services.AddScoped<IAiAnalysisService, ClaudeAiAnalysisService>();
        services.AddScoped<IAiTestGenerationService, ClaudeTestGenerationService>();

        // ── Notificaciones, evidencias y reportes ────────────────────
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddSingleton<IEvidenceStorage, FileEvidenceStorage>();
        services.AddScoped<IReportGenerator, RunReportGenerator>();

        // ── Trabajos en segundo plano (Hangfire) ─────────────────────
        var useInMemoryJobs = configuration.GetValue("Hangfire:UseInMemory", false) || useSqlite;
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();
            if (useInMemoryJobs)
                config.UseInMemoryStorage();
            else
                config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5)
                });
        });
        services.AddScoped<TestExecutionJob>();
        services.AddScoped<IBackgroundJobScheduler, HangfireJobScheduler>();

        return services;
    }
}
