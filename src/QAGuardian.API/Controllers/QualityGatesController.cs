using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.QualityGates;

namespace QAGuardian.API.Controllers;

[Authorize]
public class QualityGatesController : ApiControllerBase
{
    /// <summary>Lista los quality gates configurados.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await Mediator.Send(new GetQualityGatesQuery(), ct));

    /// <summary>Obtiene un quality gate específico con sus condiciones.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetQualityGateDetailQuery(id), ct));

    /// <summary>Crea un quality gate con sus condiciones.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Create([FromBody] CreateQualityGateCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Actualiza un quality gate existente.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQualityGateCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command with { Id = id }, ct));

    /// <summary>Elimina un quality gate (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteQualityGateCommand(id), ct));

    /// <summary>Asigna un quality gate a un proyecto.</summary>
    [HttpPost("assign")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Assign([FromBody] AssignGateToProjectCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Obtiene el historial de auditoría de cambios en un quality gate.</summary>
    [HttpGet("{id:guid}/audit-log")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> GetAuditLog(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetQualityGateAuditLogQuery(id, page, pageSize), ct));
}
