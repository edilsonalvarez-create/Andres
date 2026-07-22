using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;

namespace QAGuardian.API.Hubs;

/// <summary>
/// Hub de tiempo real: los clientes se suscriben a las ejecuciones que les interesan.
/// Sprint 11: además de autenticación, exige membresía del proyecto del TestRun (TM-01).
/// Denegación → HubException genérica (anti-enumeración).
/// </summary>
[Authorize(Policy = Policies.ViewReports)]
public class TestRunHub : Hub
{
    private readonly IProjectAccessService _access;
    private readonly ILogger<TestRunHub> _logger;

    public TestRunHub(IProjectAccessService access, ILogger<TestRunHub> logger)
    {
        _access = access;
        _logger = logger;
    }

    public static string RunGroup(Guid testRunId) => $"run:{testRunId:N}";

    public async Task SubscribeToRun(Guid testRunId)
    {
        try
        {
            await _access.EnsureCanAccessTestRunAsync(testRunId, Context.ConnectionAborted);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning(
                "Usuario {User} intentó suscribirse a un test run inaccesible o inexistente {TestRunId}",
                Context.User?.Identity?.Name, testRunId);
            throw new HubException("El test run indicado no existe.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RunGroup(testRunId));
    }

    public Task UnsubscribeFromRun(Guid testRunId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, RunGroup(testRunId));
}
