using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Approvals;

namespace QAGuardian.API.Controllers;

/// <summary>Flujo de aprobación humana (sign-off enterprise).</summary>
[Authorize]
public class ApprovalsController : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetPending([FromQuery] Guid? projectId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetPendingApprovalsQuery(projectId), ct));

    [HttpPost]
    [Authorize(Policy = Policies.ExecuteTests)]
    public async Task<IActionResult> Create([FromBody] CreateApprovalRequestCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    [HttpPost("{id:guid}/decide")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] DecideApprovalCommand command, CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));
}
