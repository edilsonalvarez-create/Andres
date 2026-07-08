using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Defects;
using QAGuardian.Domain.Enums;

namespace QAGuardian.API.Controllers;

[Authorize]
public class DefectsController : ApiControllerBase
{
    /// <summary>Lista defectos con filtros por estado y severidad.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll([FromQuery] Guid projectId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] DefectStatus? status = null,
        [FromQuery] DefectSeverity? severity = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetDefectsQuery(projectId, page, pageSize, status, severity), ct));

    /// <summary>Registra un defecto.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageDefects)]
    public async Task<IActionResult> Create([FromBody] CreateDefectCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Cambia el estado del defecto (asignar, resolver, verificar, cerrar, reabrir).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.ManageDefects)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeDefectStatusCommand command,
        CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));
}
