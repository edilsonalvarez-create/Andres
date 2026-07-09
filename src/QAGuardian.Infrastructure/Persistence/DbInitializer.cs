using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Persistence;

/// <summary>Crea el esquema y siembra datos iniciales (roles, admin, quality gate por defecto).</summary>
public class DbInitializer
{
    private readonly QAGuardianDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(QAGuardianDbContext context, IPasswordHasher hasher, ILogger<DbInitializer> logger)
    {
        _context = context;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task InitializeAsync(string adminEmail, string adminPassword, CancellationToken ct = default)
    {
        // SQL Server usa migraciones EF Core (historial versionado en __EFMigrationsHistory);
        // SQLite (desarrollo/pruebas) crea el esquema directo del modelo.
        // Bases creadas por los scripts SQL del DBA: use Database:SkipInitialization=true.
        if (_context.Database.IsSqlServer())
            await _context.Database.MigrateAsync(ct);
        else
            await _context.Database.EnsureCreatedAsync(ct);
        await SeedRolesAsync(ct);
        await SeedAdminAsync(adminEmail, adminPassword, ct);
        await SeedDefaultQualityGateAsync(ct);
        _logger.LogInformation("Base de datos inicializada correctamente.");
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var descriptions = new Dictionary<string, string>
        {
            [SystemRoles.Administrator] = "Acceso total a la plataforma",
            [SystemRoles.QA] = "Gestión y ejecución de pruebas",
            [SystemRoles.Developer] = "Consulta de resultados y corrección de defectos",
            [SystemRoles.TechLead] = "Supervisión técnica y aprobación de quality gates",
            [SystemRoles.DevOps] = "Gestión de pipelines y despliegues",
            [SystemRoles.ProductOwner] = "Consulta de dashboards y reportes",
            [SystemRoles.Auditor] = "Consulta de auditoría y evidencias (solo lectura)",
            [SystemRoles.Client] = "Consulta de reportes ejecutivos (solo lectura)"
        };

        foreach (var roleName in SystemRoles.All)
        {
            if (!await _context.Roles.AnyAsync(r => r.Name == roleName, ct))
                _context.Roles.Add(new Role(roleName, descriptions[roleName]));
        }
        await _context.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync(string adminEmail, string adminPassword, CancellationToken ct)
    {
        if (await _context.Users.AnyAsync(u => u.Email == adminEmail.ToLower(), ct)) return;

        var admin = new User(adminEmail, "Administrador del Sistema", _hasher.Hash(adminPassword));
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == SystemRoles.Administrator, ct);
        admin.AssignRole(adminRole);
        _context.Users.Add(admin);
        await _context.SaveChangesAsync(ct);
        _logger.LogWarning("Usuario administrador '{Email}' creado. Cambie la contraseña inicial.", adminEmail);
    }

    private async Task SeedDefaultQualityGateAsync(CancellationToken ct)
    {
        if (await _context.QualityGates.AnyAsync(g => g.IsDefault, ct)) return;

        var gate = new QualityGate("Gate Estándar QA Guardian", isDefault: true);
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        gate.AddCondition(GateMetric.CriticalVulnerabilities, GateOperator.Equal, 0m);
        gate.AddCondition(GateMetric.HighVulnerabilities, GateOperator.LessOrEqual, 2m, isBlocking: false);
        _context.QualityGates.Add(gate);
        await _context.SaveChangesAsync(ct);
    }
}
