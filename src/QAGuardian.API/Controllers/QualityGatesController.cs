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

    /// <summary>Crea un quality gate con sus condiciones (B6: solo Administrador).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> Create([FromBody] CreateQualityGateCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Actualiza un quality gate existente (B6: solo Administrador).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQualityGateCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command with { Id = id }, ct));

    /// <summary>Elimina un quality gate (soft delete) (B6: solo Administrador).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteQualityGateCommand(id), ct));

    /// <summary>Asigna un quality gate a un proyecto (B6: ProjectAdmin del proyecto).</summary>
    [HttpPost("assign")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Assign([FromBody] AssignGateToProjectCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Historial de auditoría de un quality gate (B6: solo Administrador).</summary>
    [HttpGet("{id:guid}/audit-log")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> GetAuditLog(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetQualityGateAuditLogQuery(id, page, pageSize), ct));
}
