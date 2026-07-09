using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                options.UseSqlite(connectionString ?? "Data Source=qaguardian.db");
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DbInitializer>();

        // ── Identidad y seguridad ────────────────────────────────────
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
        services.AddSingleton<ProcessExecutor>();
        services.AddScoped<PlaywrightTestRunner>();
        services.AddScoped<NewmanTestRunner>();
        services.AddScoped<JMeterTestRunner>();
        services.AddScoped<ZapScanRunner>();
        services.AddScoped<VisualRegressionRunner>();
        services.AddSingleton<IImageComparer, ImageSharpComparer>();
        services.AddScoped<ITestRunnerFactory, TestRunnerFactory>();

        // ── Integraciones externas ───────────────────────────────────
        services.AddScoped<IntegrationSettingResolver>();
        services.AddHttpClient<ISonarQubeClient, SonarQubeClient>();
        services.AddHttpClient<IGitHubClient, GitHubApiClient>();
        services.AddHttpClient("notifications");
        services.AddScoped<IDatabaseSchemaValidator, SqlServerSchemaValidator>();

        // ── Agente IA ────────────────────────────────────────────────
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
