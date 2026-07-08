using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Reports;

/// <summary>Genera reportes de ejecución en PDF, Excel, Word, HTML, JSON y CSV.</summary>
public class RunReportGenerator : IReportGenerator
{
    private readonly ITestRunRepository _runs;
    private readonly IProjectRepository _projects;

    static RunReportGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public RunReportGenerator(ITestRunRepository runs, IProjectRepository projects)
    {
        _runs = runs;
        _projects = projects;
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GenerateRunReportAsync(
        Guid testRunId, ReportFormat format, CancellationToken ct = default)
    {
        var run = await _runs.GetWithResultsAsync(testRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), testRunId);
        var project = await _projects.GetByIdAsync(run.ProjectId, ct);
        var projectName = project?.Name ?? "Proyecto";
        var baseName = $"reporte-{projectName.Replace(' ', '-')}-{run.Id:N}";

        return format switch
        {
            ReportFormat.Json => (GenerateJson(run, projectName), "application/json", $"{baseName}.json"),
            ReportFormat.Csv => (GenerateCsv(run), "text/csv", $"{baseName}.csv"),
            ReportFormat.Html => (GenerateHtml(run, projectName), "text/html", $"{baseName}.html"),
            ReportFormat.Excel => (GenerateExcel(run, projectName),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx"),
            ReportFormat.Pdf => (GeneratePdf(run, projectName), "application/pdf", $"{baseName}.pdf"),
            ReportFormat.Word => (GenerateWord(run, projectName),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{baseName}.docx"),
            _ => throw new NotSupportedException($"Formato no soportado: {format}")
        };
    }

    private static byte[] GenerateJson(TestRun run, string projectName)
    {
        var payload = new
        {
            proyecto = projectName,
            ejecucion = run.Id,
            tipo = run.RunType.ToString(),
            ambiente = run.Environment.ToString(),
            estado = run.Status.ToString(),
            iniciada = run.StartedAt,
            completada = run.CompletedAt,
            total = run.TotalTests,
            exitosas = run.Passed,
            fallidas = run.Failed,
            omitidas = run.Skipped,
            porcentajeExito = run.PassRatePercent,
            qualityGate = run.GateEvaluation?.Status.ToString(),
            resultados = run.Results.Select(r => new
            {
                r.Name,
                estado = r.Status.ToString(),
                duracionMs = r.DurationMs,
                error = r.ErrorMessage
            })
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static byte[] GenerateCsv(TestRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Prueba,Estado,DuracionMs,Error");
        foreach (var r in run.Results)
            sb.AppendLine($"\"{Escape(r.Name)}\",{r.Status},{r.DurationMs},\"{Escape(r.ErrorMessage)}\"");
        return Encoding.UTF8.GetBytes(sb.ToString());

        static string Escape(string? value) => (value ?? "").Replace("\"", "\"\"");
    }

    private static byte[] GenerateHtml(TestRun run, string projectName)
    {
        var rows = string.Join('\n', run.Results.Select(r =>
            $"<tr class='{(r.Status == ResultStatus.Passed ? "ok" : r.Status == ResultStatus.Failed ? "fail" : "")}'>" +
            $"<td>{System.Net.WebUtility.HtmlEncode(r.Name)}</td><td>{r.Status}</td>" +
            $"<td>{r.DurationMs} ms</td><td>{System.Net.WebUtility.HtmlEncode(r.ErrorMessage ?? "")}</td></tr>"));

        var html = $$"""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8"><title>Reporte QA Guardian</title>
            <style>
              body { font-family: 'Segoe UI', sans-serif; margin: 2rem; color: #1a1a2e; }
              h1 { color: #16213e; } table { border-collapse: collapse; width: 100%; }
              th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
              th { background: #16213e; color: white; }
              tr.ok td { background: #e8f5e9; } tr.fail td { background: #ffebee; }
              .kpi { display: inline-block; margin-right: 2rem; font-size: 1.2rem; }
            </style></head><body>
            <h1>🛡️ QA Guardian — {{System.Net.WebUtility.HtmlEncode(projectName)}}</h1>
            <p>Ejecución {{run.RunType}} en {{run.Environment}} — Estado: <strong>{{run.Status}}</strong>
               — Quality Gate: <strong>{{run.GateEvaluation?.Status.ToString() ?? "N/A"}}</strong></p>
            <div>
              <span class="kpi">Total: <strong>{{run.TotalTests}}</strong></span>
              <span class="kpi">✅ {{run.Passed}}</span>
              <span class="kpi">❌ {{run.Failed}}</span>
              <span class="kpi">⏭️ {{run.Skipped}}</span>
              <span class="kpi">Éxito: <strong>{{run.PassRatePercent}}%</strong></span>
            </div>
            <table><thead><tr><th>Prueba</th><th>Estado</th><th>Duración</th><th>Error</th></tr></thead>
            <tbody>{{rows}}</tbody></table>
            <p><em>Generado por QA Guardian el {{DateTime.UtcNow:yyyy-MM-dd HH:mm}} UTC</em></p>
            </body></html>
            """;
        return Encoding.UTF8.GetBytes(html);
    }

    private static byte[] GenerateExcel(TestRun run, string projectName)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Resumen");
        summary.Cell(1, 1).Value = "QA Guardian — Reporte de Ejecución";
        summary.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        summary.Cell(3, 1).Value = "Proyecto"; summary.Cell(3, 2).Value = projectName;
        summary.Cell(4, 1).Value = "Tipo"; summary.Cell(4, 2).Value = run.RunType.ToString();
        summary.Cell(5, 1).Value = "Ambiente"; summary.Cell(5, 2).Value = run.Environment.ToString();
        summary.Cell(6, 1).Value = "Total"; summary.Cell(6, 2).Value = run.TotalTests;
        summary.Cell(7, 1).Value = "Exitosas"; summary.Cell(7, 2).Value = run.Passed;
        summary.Cell(8, 1).Value = "Fallidas"; summary.Cell(8, 2).Value = run.Failed;
        summary.Cell(9, 1).Value = "% Éxito"; summary.Cell(9, 2).Value = run.PassRatePercent;
        summary.Cell(10, 1).Value = "Quality Gate";
        summary.Cell(10, 2).Value = run.GateEvaluation?.Status.ToString() ?? "N/A";
        summary.Columns().AdjustToContents();

        var detail = workbook.Worksheets.Add("Detalle");
        detail.Cell(1, 1).Value = "Prueba";
        detail.Cell(1, 2).Value = "Estado";
        detail.Cell(1, 3).Value = "Duración (ms)";
        detail.Cell(1, 4).Value = "Error";
        detail.Row(1).Style.Font.SetBold();
        var row = 2;
        foreach (var r in run.Results)
        {
            detail.Cell(row, 1).Value = r.Name;
            detail.Cell(row, 2).Value = r.Status.ToString();
            detail.Cell(row, 3).Value = r.DurationMs;
            detail.Cell(row, 4).Value = r.ErrorMessage ?? "";
            row++;
        }
        detail.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GeneratePdf(TestRun run, string projectName)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Text($"🛡️ QA Guardian — {projectName}")
                    .FontSize(18).Bold().FontColor(Colors.Indigo.Darken3);
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Ejecución {run.RunType} · Ambiente {run.Environment} · Estado {run.Status}");
                    col.Item().Text($"Quality Gate: {run.GateEvaluation?.Status.ToString() ?? "N/A"}").Bold();
                    col.Item().Text($"Total: {run.TotalTests} · ✅ {run.Passed} · ❌ {run.Failed} · " +
                                    $"Omitidas: {run.Skipped} · Éxito: {run.PassRatePercent}%");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(4);
                        });
                        table.Header(header =>
                        {
                            foreach (var title in new[] { "Prueba", "Estado", "Duración", "Error" })
                                header.Cell().Background(Colors.Indigo.Darken3).Padding(4)
                                    .Text(title).FontColor(Colors.White).Bold();
                        });
                        foreach (var r in run.Results)
                        {
                            var bg = r.Status == ResultStatus.Failed ? Colors.Red.Lighten4 : Colors.White;
                            table.Cell().Background(bg).Padding(4).Text(r.Name).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(r.Status.ToString()).FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text($"{r.DurationMs} ms").FontSize(9);
                            table.Cell().Background(bg).Padding(4).Text(r.ErrorMessage ?? "").FontSize(8);
                        }
                    });
                });
                page.Footer().AlignCenter()
                    .Text($"Generado por QA Guardian — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });
        });
        return document.GeneratePdf();
    }

    private static byte[] GenerateWord(TestRun run, string projectName)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            var body = new Body();
            body.AppendChild(MakeParagraph($"QA Guardian — {projectName}", bold: true, size: 32));
            body.AppendChild(MakeParagraph($"Ejecución {run.RunType} · Ambiente {run.Environment} · Estado {run.Status}"));
            body.AppendChild(MakeParagraph($"Quality Gate: {run.GateEvaluation?.Status.ToString() ?? "N/A"}", bold: true));
            body.AppendChild(MakeParagraph($"Total: {run.TotalTests} | Exitosas: {run.Passed} | Fallidas: {run.Failed} | " +
                                           $"Éxito: {run.PassRatePercent}%"));
            body.AppendChild(MakeParagraph(""));
            body.AppendChild(MakeParagraph("Detalle de resultados:", bold: true));
            foreach (var r in run.Results)
                body.AppendChild(MakeParagraph(
                    $"[{r.Status}] {r.Name} ({r.DurationMs} ms)" +
                    (r.ErrorMessage is null ? "" : $" — {r.ErrorMessage}")));
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document(body);
            mainPart.Document.Save();
        }
        return stream.ToArray();

        static Paragraph MakeParagraph(string text, bool bold = false, int size = 22)
        {
            var runProps = new RunProperties(new FontSize { Val = size.ToString() });
            if (bold) runProps.AppendChild(new Bold());
            return new Paragraph(new Run(runProps, new Text(text)));
        }
    }
}
