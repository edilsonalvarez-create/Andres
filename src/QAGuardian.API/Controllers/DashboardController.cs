using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Dashboard;

namespace QAGuardian.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    /// <summary>KPIs ejecutivos: cobertura, ejecuciones, defectos, tendencia, heatmap y calidad.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? projectId = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetDashboardStatsQuery(projectId, from, to), ct));

    /// <summary>Exporta el dashboard ejecutivo en PDF o Excel.</summary>
    [HttpGet("report")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> ExportReport([FromQuery] ReportFormat format,
        [FromServices] IReportGenerator generator, [FromQuery] Guid? projectId = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var stats = await Mediator.Send(new GetDashboardStatsQuery(projectId, from, to), ct);
        var (content, contentType, fileName) = generator.GenerateDashboardReport(
            stats, "QA Guardian — Dashboard ejecutivo", format);
        return File(content, contentType, fileName);
    }
}
