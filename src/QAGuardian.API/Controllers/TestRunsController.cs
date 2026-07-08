using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.TestRuns;

namespace QAGuardian.API.Controllers;

[Authorize]
public class TestRunsController : ApiControllerBase
{
    /// <summary>Lista las ejecuciones de un proyecto.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll([FromQuery] Guid projectId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetTestRunsQuery(projectId, page, pageSize), ct));

    /// <summary>Obtiene el detalle de una ejecución con resultados y evidencias.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var (run, results) = await Mediator.Send(new GetTestRunDetailQuery(id), ct);
        return Ok(new { run, results });
    }

    /// <summary>Inicia una nueva ejecución de pruebas (se procesa en segundo plano).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ExecuteTests)]
    public async Task<IActionResult> Start([FromBody] StartTestRunCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Cancela una ejecución pendiente o en curso.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.ExecuteTests)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new CancelTestRunCommand(id), ct));

    /// <summary>Descarga el reporte de la ejecución en el formato indicado (pdf, excel, word, html, json, csv).</summary>
    [HttpGet("{id:guid}/report")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> DownloadReport(Guid id, [FromQuery] ReportFormat format,
        [FromServices] IReportGenerator generator, CancellationToken ct)
    {
        var (content, contentType, fileName) = await generator.GenerateRunReportAsync(id, format, ct);
        return File(content, contentType, fileName);
    }

    /// <summary>Descarga una evidencia (screenshot, video, log) por ruta relativa.</summary>
    [HttpGet("evidence")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> DownloadEvidence([FromQuery] string path,
        [FromServices] IEvidenceStorage storage, CancellationToken ct)
    {
        var stream = await storage.OpenReadAsync(path, ct);
        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webm" => "video/webm",
            ".mp4" => "video/mp4",
            ".json" => "application/json",
            ".html" => "text/html",
            _ => "application/octet-stream"
        };
        return File(stream, contentType, Path.GetFileName(path));
    }
}
