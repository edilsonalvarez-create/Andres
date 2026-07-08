using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Projects;

namespace QAGuardian.API.Controllers;

[Authorize]
public class ProjectsController : ApiControllerBase
{
    /// <summary>Lista proyectos con paginación y búsqueda.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetProjectsQuery(page, pageSize, search), ct));

    /// <summary>Obtiene un proyecto por identificador.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetProjectByIdQuery(id), ct));

    /// <summary>Crea un proyecto.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Create([FromBody] CreateProjectCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Actualiza un proyecto.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectCommand command, CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));

    /// <summary>Elimina (lógicamente) un proyecto.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteProjectCommand(id), ct));

    /// <summary>Agrega un módulo al proyecto.</summary>
    [HttpPost("{id:guid}/modules")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> AddModule(Guid id, [FromBody] AddModuleCommand command, CancellationToken ct)
        => id != command.ProjectId
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));
}
