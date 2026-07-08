using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Notifications;
using QAGuardian.Application.Features.Users;

namespace QAGuardian.API.Controllers;

[Authorize]
[Route("api/v{version:apiVersion}/admin")]
public class AdministrationController : ApiControllerBase
{
    /// <summary>Lista usuarios con sus roles.</summary>
    [HttpGet("users")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetUsersQuery(page, pageSize), ct));

    /// <summary>Reemplaza los roles de un usuario.</summary>
    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> UpdateRoles(Guid id, [FromBody] List<string> roles, CancellationToken ct)
        => FromResult(await Mediator.Send(new UpdateUserRolesCommand(id, roles), ct));

    /// <summary>Lista los canales de notificación configurados.</summary>
    [HttpGet("notification-channels")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> GetChannels([FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetNotificationChannelsQuery(projectId), ct));

    /// <summary>Crea o actualiza un canal de notificación (correo, Teams, Slack, Discord, Telegram).</summary>
    [HttpPost("notification-channels")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> UpsertChannel([FromBody] UpsertNotificationChannelCommand command,
        CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
