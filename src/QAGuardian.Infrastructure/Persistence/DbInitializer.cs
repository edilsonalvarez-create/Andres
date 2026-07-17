using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Persistence;

/// <summary>
/// Aplica migraciones EF (solo cuando se solicita) y siembra datos iniciales
/// (roles, admin, quality gate por defecto). El esquema es propiedad de
/// <c>Migrations/</c>; no hay parches SQL ad-hoc divergentes.
/// </summary>
public class DbInitializer
{
    private readonly QAGuardianDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        QAGuardianDbContext context,
        IPasswordHasher hasher,
        IHostEnvironment environment,
        ILogger<DbInitializer> logger)
    {
        _context = context;
        _hasher = hasher;
        _environment = environment;
        _logger = logger;
    }

    /// <param name="applyMigrations">
    /// Si es true, ejecuta <c>Database.MigrateAsync</c>.
    /// En Production/QA/Staging el host debe pasar false; el esquema se aplica
    /// con el job <c>dotnet ef database update</c> (ver Manual de Instalación).
    /// </param>
    public async Task InitializeAsync(
        string adminEmail,
        string adminPassword,
        bool applyMigrations = false,
        CancellationToken ct = default)
    {
        if (applyMigrations)
        {
            // SQL Server: migraciones EF = fuente de verdad (job en prod; opcional en Development).
            // SQLite: EnsureCreated desde el modelo actual. Las migraciones se generan para SQL Server
            // (nvarchar(max), etc.) y no son aplicables tal cual en SQLite — sin parches EnsureSprint*.
            if (_context.Database.IsSqlServer())
            {
                _logger.LogInformation("Aplicando migraciones EF Core (Database:ApplyMigrationsOnStartup).");
                await _context.Database.MigrateAsync(ct);
            }
            else
            {
                _logger.LogInformation("SQLite/dev: EnsureCreated desde el modelo (sin parches ad-hoc).");
                await _context.Database.EnsureCreatedAsync(ct);
            }
        }
        else
        {
            _logger.LogInformation(
                "Omitiendo Migrate/EnsureCreated en este proceso. " +
                "El esquema debe existir vía 'dotnet ef database update' o el servicio compose migrate.");
        }

        await SeedRolesAsync(ct);
        await SeedAdminAsync(adminEmail, adminPassword, ct);
        await SeedDefaultQualityGateAsync(ct);
        await SeedProjectMembersAsync(adminEmail, ct);
        _logger.LogInformation("Base de datos inicializada correctamente (seed).");
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

    /// <summary>Contraseñas públicamente documentadas que no deben sembrarse fuera de Development
    /// (defensa en profundidad; el arranque en <c>Program.cs</c> aplica la misma regla).</summary>
    private static readonly string[] KnownPublicDefaultPasswords = ["QaGuardian.2026!"];

    private async Task SeedAdminAsync(string adminEmail, string adminPassword, CancellationToken ct)
    {
        var normalizedEmail = adminEmail.ToLower();
        var existing = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (existing is not null)
        {
            // En desarrollo local, mantener el admin alineado con Seed:AdminPassword para evitar
            // desincronización tras cambiar user-secrets o reutilizar una base SQLite antigua.
            if (_environment.IsDevelopment())
                await SyncDevelopmentAdminAsync(existing, adminPassword, ct);
            return;
        }

        if (!_environment.IsDevelopment() && KnownPublicDefaultPasswords.Contains(adminPassword))
            throw new InvalidOperationException(
                "La contraseña de administrador coincide con un valor públicamente conocido. " +
                "Configure una contraseña única para este ambiente.");

        var admin = new User(adminEmail, "Administrador del Sistema", _hasher.Hash(adminPassword));
        var adminRole = await _context.Roles.FirstAsync(r => r.Name == SystemRoles.Administrator, ct);
        admin.AssignRole(adminRole);
        _context.Users.Add(admin);
        await _context.SaveChangesAsync(ct);
        _logger.LogWarning("Usuario administrador '{Email}' creado. Cambie la contraseña inicial.", adminEmail);
    }

    private async Task SyncDevelopmentAdminAsync(User admin, string adminPassword, CancellationToken ct)
    {
        var changed = false;

        if (!_hasher.Verify(adminPassword, admin.PasswordHash))
        {
            admin.ChangePassword(_hasher.Hash(adminPassword));
            changed = true;
            _logger.LogWarning(
                "Development: contraseña del administrador sincronizada con Seed:AdminPassword.");
        }

        if (admin.IsLocked)
        {
            admin.RegisterSuccessfulLogin();
            changed = true;
            _logger.LogWarning("Development: bloqueo por intentos fallidos del administrador restablecido.");
        }

        if (changed)
            await _context.SaveChangesAsync(ct);
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

    /// <summary>
    /// Admin → ProjectAdmin en todos los proyectos. Idempotente (no duplica membresías).
    /// </summary>
    private async Task SeedProjectMembersAsync(string adminEmail, CancellationToken ct)
    {
        var admin = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == adminEmail.ToLower(), ct);
        if (admin is null)
            return;

        var projectIds = await _context.Projects
            .Where(p => !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (projectIds.Count == 0)
            return;

        var existing = await _context.ProjectMembers
            .Where(m => m.UserId == admin.Id && !m.IsDeleted)
            .Select(m => m.ProjectId)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var added = 0;
        foreach (var projectId in projectIds)
        {
            if (existingSet.Contains(projectId))
                continue;

            _context.ProjectMembers.Add(
                new ProjectMember(projectId, admin.Id, RoleInProject.ProjectAdmin));
            added++;
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Seed ProjectMembers: admin '{Email}' asignado a {Count} proyecto(s).",
                adminEmail, added);
        }
    }
}
