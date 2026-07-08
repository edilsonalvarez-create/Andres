using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using QAGuardian.API.Hubs;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.API.Services;

/// <summary>Expone el usuario autenticado del HttpContext a las capas internas.</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
                            ?? Principal?.FindFirstValue("email");

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}

/// <summary>Publica el progreso de las ejecuciones en tiempo real vía SignalR.</summary>
public class SignalRRunProgressNotifier : IRunProgressNotifier
{
    private readonly IHubContext<TestRunHub> _hub;

    public SignalRRunProgressNotifier(IHubContext<TestRunHub> hub) => _hub = hub;

    public Task RunStatusChangedAsync(Guid testRunId, string status, CancellationToken ct = default)
        => _hub.Clients.Group(TestRunHub.RunGroup(testRunId))
            .SendAsync("runStatusChanged", new { testRunId, status }, ct);

    public Task RunCompletedAsync(Guid testRunId, int passed, int failed, string gateStatus, CancellationToken ct = default)
        => _hub.Clients.Group(TestRunHub.RunGroup(testRunId))
            .SendAsync("runCompleted", new { testRunId, passed, failed, gateStatus }, ct);
}
