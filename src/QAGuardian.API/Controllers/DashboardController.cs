using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Dashboard;

namespace QAGuardian.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    /// <summary>KPIs ejecutivos: cobertura, ejecuciones, defectos, tendencia y calidad.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? projectId = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetDashboardStatsQuery(projectId), ct));
}
