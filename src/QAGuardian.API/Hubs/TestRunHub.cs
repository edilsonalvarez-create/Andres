using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace QAGuardian.API.Hubs;

/// <summary>Hub de tiempo real: los clientes se suscriben a las ejecuciones que les interesan.</summary>
[Authorize]
public class TestRunHub : Hub
{
    public static string RunGroup(Guid testRunId) => $"run:{testRunId:N}";

    public Task SubscribeToRun(Guid testRunId)
        => Groups.AddToGroupAsync(Context.ConnectionId, RunGroup(testRunId));

    public Task UnsubscribeFromRun(Guid testRunId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RunGroup(testRunId));
}
