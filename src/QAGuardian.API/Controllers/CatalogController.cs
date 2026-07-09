using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Catalog;

namespace QAGuardian.API.Controllers;

/// <summary>Administración de módulos, requerimientos, historias y versiones del proyecto.</summary>
[Authorize]
public class CatalogController : ApiControllerBase
{
    // ── Módulos ──────────────────────────────────────────────
    [HttpGet("projects/{projectId:guid}/modules")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetModules(Guid projectId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetModulesByProjectQuery(projectId), ct));

    // ── Requerimientos ───────────────────────────────────────
    [HttpGet("modules/{moduleId:guid}/requirements")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetRequirements(Guid moduleId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetRequirementsByModuleQuery(moduleId), ct));

    [HttpPost("requirements")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> CreateRequirement([FromBody] CreateRequirementCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    // ── Historias de usuario ─────────────────────────────────
    [HttpGet("requirements/{requirementId:guid}/stories")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetStories(Guid requirementId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetUserStoriesByRequirementQuery(requirementId), ct));

    [HttpPost("stories")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> CreateStory([FromBody] CreateUserStoryCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    // ── Versiones ────────────────────────────────────────────
    [HttpGet("projects/{projectId:guid}/versions")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetVersions(Guid projectId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetProjectVersionsQuery(projectId), ct));

    [HttpPost("versions")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> CreateVersion([FromBody] CreateProjectVersionCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPost("versions/{id:guid}/release")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> ReleaseVersion(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new ReleaseProjectVersionCommand(id), ct));
}
