using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Identity;

/// <summary>
/// Enforcement ACL por proyecto. Admin global bypass; resto requiere ProjectMember activo.
/// Sin acceso → NotFoundException (anti-enum), nunca revela existencia del recurso ajeno.
/// B6 / ADR-013: <see cref="EnsureCanAdministerProjectAsync"/> exige ProjectAdmin.
/// </summary>
public sealed class ProjectAccessService : IProjectAccessService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectMemberRepository _members;
    private readonly IProjectRepository _projects;
    private readonly ITestRunRepository _testRuns;

    public ProjectAccessService(
        ICurrentUserService currentUser,
        IProjectMemberRepository members,
        IProjectRepository projects,
        ITestRunRepository testRuns)
    {
        _currentUser = currentUser;
        _members = members;
        _projects = projects;
        _testRuns = testRuns;
    }

    public bool IsGlobalAdministrator() => IsGlobalAdmin();

    public async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty)
            return false;

        if (IsGlobalAdmin())
            return await _projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, ct);

        var userId = _currentUser.UserId;
        if (userId is null)
            return false;

        return await _members.IsActiveMemberAsync(projectId, userId.Value, ct)
               && await _projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, ct);
    }

    public async Task EnsureCanAccessProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!await CanAccessProjectAsync(projectId, ct))
            throw new NotFoundException(nameof(Project), projectId);
    }

    public async Task EnsureCanAdministerProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (projectId == Guid.Empty
            || !await _projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, ct))
            throw new NotFoundException(nameof(Project), projectId);

        if (IsGlobalAdmin())
            return;

        var userId = _currentUser.UserId;
        if (userId is null)
            throw new NotFoundException(nameof(Project), projectId);

        var memberships = await _members.ListByUserAsync(userId.Value, ct);
        var membership = memberships.FirstOrDefault(m =>
            m.ProjectId == projectId && m.IsActive && m.RoleInProject == RoleInProject.ProjectAdmin);

        if (membership is null)
            throw new NotFoundException(nameof(Project), projectId);
    }

    public async Task<IReadOnlyList<Guid>> ListAccessibleProjectIdsAsync(CancellationToken ct = default)
    {
        if (IsGlobalAdmin())
        {
            var all = await _projects.ListAsync(p => !p.IsDeleted, ct);
            return all.Select(p => p.Id).ToList();
        }

        var userId = _currentUser.UserId;
        if (userId is null)
            return [];

        var memberships = await _members.ListByUserAsync(userId.Value, ct);
        return memberships
            .Where(m => m.IsActive)
            .Select(m => m.ProjectId)
            .Distinct()
            .ToList();
    }

    public async Task EnsureCanAccessTestRunAsync(Guid testRunId, CancellationToken ct = default)
    {
        _ = await GetAccessibleTestRunProjectIdAsync(testRunId, ct);
    }

    public async Task<Guid> GetAccessibleTestRunProjectIdAsync(Guid testRunId, CancellationToken ct = default)
    {
        var run = await _testRuns.GetByIdAsync(testRunId, ct);
        if (run is null || run.IsDeleted)
            throw new NotFoundException(nameof(TestRun), testRunId);

        await EnsureCanAccessProjectAsync(run.ProjectId, ct);
        return run.ProjectId;
    }

    private bool IsGlobalAdmin() => _currentUser.IsInRole(SystemRoles.Administrator);
}
