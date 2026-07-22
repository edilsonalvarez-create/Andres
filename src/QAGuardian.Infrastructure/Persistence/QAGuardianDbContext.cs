using Microsoft.EntityFrameworkCore;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Infrastructure.Persistence;

public class QAGuardianDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;

    public QAGuardianDbContext(DbContextOptions<QAGuardianDbContext> options,
        ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;

        // Las entidades del dominio generan su Guid en el constructor, por lo que EF
        // marca como "Modified" a los hijos nuevos descubiertos por fix-up de navegación
        // (p. ej. run.AddResult, user.AddRefreshToken). Los corregimos a "Added":
        // una entidad que empieza a rastrearse como Modified sin venir de una consulta
        // siempre es una entidad recién creada por un método del agregado.
        ChangeTracker.Tracked += (_, e) =>
        {
            if (!e.FromQuery && e.Entry.State == EntityState.Modified)
                e.Entry.State = EntityState.Added;
        };
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<UserStory> UserStories => Set<UserStory>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<TestStep> TestSteps => Set<TestStep>();
    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<Evidence> Evidences => Set<Evidence>();
    public DbSet<Defect> Defects => Set<Defect>();
    public DbSet<QualityGate> QualityGates => Set<QualityGate>();
    public DbSet<QualityGateCondition> QualityGateConditions => Set<QualityGateCondition>();
    public DbSet<QualityGateEvaluation> QualityGateEvaluations => Set<QualityGateEvaluation>();
    public DbSet<SecurityFinding> SecurityFindings => Set<SecurityFinding>();
    public DbSet<AiAnalysis> AiAnalyses => Set<AiAnalysis>();
    public DbSet<PipelineExecution> PipelineExecutions => Set<PipelineExecution>();
    public DbSet<DatabaseValidationRun> DatabaseValidationRuns => Set<DatabaseValidationRun>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationChannelConfig> NotificationChannels => Set<NotificationChannelConfig>();
    public DbSet<IntegrationSetting> IntegrationSettings => Set<IntegrationSetting>();
    public DbSet<VisualBaseline> VisualBaselines => Set<VisualBaseline>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectDatabaseEnvironment> ProjectDatabaseEnvironments => Set<ProjectDatabaseEnvironment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.Property(p => p.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2000);
            e.Property(p => p.RepositoryUrl).HasMaxLength(500);
            e.HasMany(p => p.Modules).WithOne(m => m.Project).HasForeignKey(m => m.ProjectId);
            e.HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.ProjectId);
            e.HasOne(p => p.QualityGate).WithMany().HasForeignKey(p => p.QualityGateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Module>(e =>
        {
            e.ToTable("Modules");
            e.Property(m => m.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(m => new { m.ProjectId, m.Name });
            e.HasMany(m => m.Requirements).WithOne(r => r.Module).HasForeignKey(r => r.ModuleId);
        });

        modelBuilder.Entity<ProjectVersion>(e =>
        {
            e.ToTable("ProjectVersions");
            e.Property(v => v.Number).HasMaxLength(50).IsRequired();
            e.HasIndex(v => new { v.ProjectId, v.Number }).IsUnique();
        });

        modelBuilder.Entity<Requirement>(e =>
        {
            e.ToTable("Requirements");
            e.Property(r => r.Code).HasMaxLength(30).IsRequired();
            e.Property(r => r.Title).HasMaxLength(300).IsRequired();
            e.HasMany(r => r.UserStories).WithOne(s => s.Requirement).HasForeignKey(s => s.RequirementId);
        });

        modelBuilder.Entity<UserStory>(e =>
        {
            e.ToTable("UserStories");
            e.Property(s => s.Title).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<TestCase>(e =>
        {
            e.ToTable("TestCases");
            e.Property(t => t.Code).HasMaxLength(30).IsRequired();
            e.HasIndex(t => new { t.ProjectId, t.Code }).IsUnique();
            e.Property(t => t.Title).HasMaxLength(300).IsRequired();
            e.Property(t => t.AutomationScriptPath).HasMaxLength(500);
            e.Property(t => t.Tags).HasMaxLength(500);
            e.HasIndex(t => t.ProjectId);
            e.HasIndex(t => t.Type);
            e.HasIndex(t => t.UserStoryId);
            e.HasMany(t => t.Steps).WithOne().HasForeignKey(s => s.TestCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            var nav = e.Metadata.FindNavigation(nameof(TestCase.Steps));
            nav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TestStep>(e =>
        {
            e.ToTable("TestSteps");
            e.Property(s => s.Action).HasMaxLength(1000).IsRequired();
            e.Property(s => s.ExpectedResult).HasMaxLength(1000);
        });

        modelBuilder.Entity<TestRun>(e =>
        {
            e.ToTable("TestRuns");
            e.Property(r => r.TriggeredBy).HasMaxLength(256).IsRequired();
            e.Property(r => r.CommitSha).HasMaxLength(64);
            e.HasIndex(r => r.ProjectId);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.CreatedAt);
            e.Ignore(r => r.TotalTests);
            e.Ignore(r => r.Passed);
            e.Ignore(r => r.Failed);
            e.Ignore(r => r.Skipped);
            e.Ignore(r => r.PassRatePercent);
            e.Ignore(r => r.DurationSeconds);
            e.HasMany(r => r.Results).WithOne().HasForeignKey(res => res.TestRunId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.GateEvaluation).WithOne().HasForeignKey<QualityGateEvaluation>(ev => ev.TestRunId);
            var nav = e.Metadata.FindNavigation(nameof(TestRun.Results));
            nav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TestResult>(e =>
        {
            e.ToTable("TestResults");
            e.Property(r => r.Name).HasMaxLength(500).IsRequired();
            e.HasIndex(r => r.TestRunId);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.TestCaseId);
            e.HasMany(r => r.Evidences).WithOne().HasForeignKey(ev => ev.TestResultId)
                .OnDelete(DeleteBehavior.Cascade);
            var nav = e.Metadata.FindNavigation(nameof(TestResult.Evidences));
            nav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Evidence>(e =>
        {
            e.ToTable("Evidences");
            e.Property(ev => ev.FilePath).HasMaxLength(600).IsRequired();
            e.Property(ev => ev.ContentType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Defect>(e =>
        {
            e.ToTable("Defects");
            e.Property(d => d.Code).HasMaxLength(30).IsRequired();
            e.HasIndex(d => new { d.ProjectId, d.Code }).IsUnique();
            e.Property(d => d.Title).HasMaxLength(300).IsRequired();
            e.Property(d => d.Sprint).HasMaxLength(50);
            e.Property(d => d.Version).HasMaxLength(50);
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.Severity);
        });

        modelBuilder.Entity<QualityGate>(e =>
        {
            e.ToTable("QualityGates");
            e.Property(g => g.Name).HasMaxLength(150).IsRequired();
            e.HasMany(g => g.Conditions).WithOne().HasForeignKey(c => c.QualityGateId)
                .OnDelete(DeleteBehavior.Cascade);
            var nav = e.Metadata.FindNavigation(nameof(QualityGate.Conditions));
            nav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<QualityGateCondition>(e =>
        {
            e.ToTable("QualityGateConditions");
            e.Property(c => c.Threshold).HasPrecision(18, 4);
        });

        modelBuilder.Entity<QualityGateEvaluation>(e =>
        {
            e.ToTable("QualityGateEvaluations");
            e.Property(ev => ev.DetailsJson).IsRequired();
            e.Ignore(ev => ev.DeploymentApproved);
        });

        modelBuilder.Entity<SecurityFinding>(e =>
        {
            e.ToTable("SecurityFindings");
            e.Property(f => f.Name).HasMaxLength(300).IsRequired();
            e.Property(f => f.Category).HasMaxLength(100).IsRequired();
            e.Property(f => f.Url).HasMaxLength(1000);
            e.Property(f => f.CweId).HasMaxLength(20);
            e.HasIndex(f => f.TestRunId);
            e.HasIndex(f => f.Risk);
        });

        modelBuilder.Entity<AiAnalysis>(e =>
        {
            e.ToTable("AiAnalyses");
            e.Property(a => a.SuggestedOwnerRole).HasMaxLength(50);
            e.Property(a => a.ModelUsed).HasMaxLength(100);
            e.Property(a => a.EstimatedHours).HasPrecision(8, 2);
            e.Property(a => a.EvidenceQuote).HasMaxLength(2000);
            e.HasIndex(a => a.TestResultId);
        });

        modelBuilder.Entity<PipelineExecution>(e =>
        {
            e.ToTable("PipelineExecutions");
            e.Property(p => p.ExternalRunId).HasMaxLength(100).IsRequired();
            e.Property(p => p.Branch).HasMaxLength(200).IsRequired();
            e.Property(p => p.CommitSha).HasMaxLength(64).IsRequired();
            e.Property(p => p.Url).HasMaxLength(500);
        });

        modelBuilder.Entity<DatabaseValidationRun>(e =>
        {
            e.ToTable("DatabaseValidationRuns");
            e.Property(v => v.SourceEnvironment).HasMaxLength(50).IsRequired();
            e.Property(v => v.TargetEnvironment).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            e.HasMany(u => u.Roles).WithMany().UsingEntity(j => j.ToTable("UserRoles"));
            e.HasMany(u => u.RefreshTokens).WithOne().HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            var rolesNav = e.Metadata.FindNavigation(nameof(User.Roles));
            rolesNav?.SetPropertyAccessMode(PropertyAccessMode.Field);
            var tokensNav = e.Metadata.FindNavigation(nameof(User.RefreshTokens));
            tokensNav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.Property(t => t.Token).HasMaxLength(200).IsRequired();
            e.HasIndex(t => t.Token);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(a => a.UserEmail).HasMaxLength(256).IsRequired();
            e.Property(a => a.Action).HasMaxLength(50).IsRequired();
            e.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.EntityName);
        });

        modelBuilder.Entity<NotificationChannelConfig>(e =>
        {
            e.ToTable("NotificationChannels");
            e.Property(n => n.Target).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<IntegrationSetting>(e =>
        {
            e.ToTable("IntegrationSettings");
            e.Property(i => i.BaseUrl).HasMaxLength(500).IsRequired();
            e.HasIndex(i => new { i.ProjectId, i.Type }).IsUnique();
        });

        modelBuilder.Entity<VisualBaseline>(e =>
        {
            e.ToTable("VisualBaselines");
            e.Property(v => v.BaselineKey).HasMaxLength(100).IsRequired();
            e.Property(v => v.BaselinePath).HasMaxLength(600).IsRequired();
            e.Property(v => v.ThresholdPercent).HasPrecision(6, 4);
            e.HasIndex(v => new { v.ProjectId, v.BaselineKey }).IsUnique();
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.ToTable("ApprovalRequests");
            e.Property(a => a.Title).HasMaxLength(300).IsRequired();
            e.Property(a => a.Comment).HasMaxLength(2000);
            e.Property(a => a.DecisionComment).HasMaxLength(2000);
            e.HasIndex(a => new { a.Status, a.ProjectId });
            e.HasIndex(a => new { a.TargetEntityId, a.Type, a.Status });
        });

        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.ToTable("ProjectMembers");
            // Sin FK navigations (mismo patrón que ApprovalRequest): evita acoplar seed/migración
            // y permite filas de membresía mientras se crean proyectos.
            e.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
            e.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<ProjectDatabaseEnvironment>(e =>
        {
            e.ToTable("ProjectDatabaseEnvironments");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.EncryptedConnectionString).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            e.HasIndex(x => x.ProjectId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        var userEmail = _currentUser?.Email ?? "system";
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userEmail;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = userEmail;
                    break;
            }
        }
    }
}
