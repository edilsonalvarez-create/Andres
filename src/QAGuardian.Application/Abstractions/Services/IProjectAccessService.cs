namespace QAGuardian.Application.Abstractions.Services;

/// <summary>
/// Autorización por proyecto (TM-01 / Sprint 11 / B6 ADR-013).
/// Combina rol global <c>Administrador</c> (bypass) con membresía <see cref="Domain.Entities.ProjectMember"/>.
/// Denegación: <see cref="Domain.Common.NotFoundException"/> (404 anti-enumeración).
/// </summary>
public interface IProjectAccessService
{
    /// <summary>True si el usuario puede ver/actuar sobre el proyecto (admin bypass o member activo).</summary>
    Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Exige acceso al proyecto; si no → 404 (recurso inexistente o no autorizado).</summary>
    Task EnsureCanAccessProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Exige Admin global o <c>RoleInProject.ProjectAdmin</c> activo en el proyecto (B6 / ADR-013).
    /// Denegación → 404 anti-enumeración.
    /// </summary>
    Task EnsureCanAdministerProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>True si el usuario actual tiene el rol global Administrador.</summary>
    bool IsGlobalAdministrator();

    /// <summary>Ids de proyectos accesibles. Admin global: todos los no borrados.</summary>
    Task<IReadOnlyList<Guid>> ListAccessibleProjectIdsAsync(CancellationToken ct = default);

    /// <summary>Resuelve el TestRun y exige acceso a su ProjectId; si no → 404.</summary>
    Task EnsureCanAccessTestRunAsync(Guid testRunId, CancellationToken ct = default);

    /// <summary>Devuelve el ProjectId del run tras validar acceso (o 404).</summary>
    Task<Guid> GetAccessibleTestRunProjectIdAsync(Guid testRunId, CancellationToken ct = default);
}
