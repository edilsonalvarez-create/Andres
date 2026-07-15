using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.AuditLog;

namespace QAGuardian.API.Controllers;

[Authorize]
public class AuditLogController : ApiControllerBase
{
    /// <summary>Obtiene el historial de auditoría (ISO 27001 A.12.4). El filtro busca coincidencia
    /// parcial sobre la ruta HTTP auditada (ej: "qualitygates" o un Guid específico).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> GetLog(
        [FromQuery] string? pathContains,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAuditLogQuery(pathContains, page, pageSize), ct);
        return Ok(result);
    }
}
