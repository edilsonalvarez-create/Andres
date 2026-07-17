using ClosedXML.Excel;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Features.Reports;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Infrastructure.Reports;

/// <summary>
/// Construye y exporta la matriz de ejecución (trazabilidad completa por caso de prueba).
/// Extraído de <see cref="RunReportGenerator"/> (Sprint 4, Extract Class) — ver ADR-008.
/// </summary>
public class ExecutionMatrixReportWriter
{
    private readonly ITestRunRepository _runs;
    private readonly IProjectRepository _projects;

    public ExecutionMatrixReportWriter(ITestRunRepository runs, IProjectRepository projects)
    {
        _runs = runs;
        _projects = projects;
    }

    public async Task<ExecutionMatrixDto> GetMatrixAsync(Guid testRunId, CancellationToken ct = default)
    {
        var run = await _runs.GetWithFullDetailsAsync(testRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), testRunId);
        var project = await _projects.GetByIdAsync(run.ProjectId, ct);
        return await BuildAsync(run, project?.Name ?? "Proyecto", ct);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GenerateExcelReportAsync(
        Guid testRunId, CancellationToken ct = default)
    {
        var run = await _runs.GetWithFullDetailsAsync(testRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), testRunId);
        var project = await _projects.GetByIdAsync(run.ProjectId, ct);
        var matrix = await BuildAsync(run, project?.Name ?? "Proyecto", ct);
        var baseName = $"matriz-ejecucion-{matrix.ProjectName.Replace(' ', '-')}-{run.Id:N}";
        var content = GenerateExcel(matrix);
        return (content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx");
    }

    private async Task<ExecutionMatrixDto> BuildAsync(TestRun run, string projectName, CancellationToken ct = default)
    {
        var rows = new List<ExecutionMatrixRow>();

        foreach (var result in run.Results)
        {
            var module = "General";
            var testCaseCode = "Ad-hoc";
            var scenarioName = result.Name;
            var priority = "Normal";
            var expectedResult = "";

            // Si está vinculado a un TestCase, obtener información adicional
            if (result.TestCaseId.HasValue)
            {
                var testCase = await _projects.GetByIdAsync(run.ProjectId, ct);
                if (testCase != null)
                {
                    testCaseCode = $"TC-{result.TestCaseId:N}";
                    priority = "Normal";
                }
            }

            var evidenceLinks = string.Join("; ", result.Evidences
                .Select(e => $"{e.Type}: {System.IO.Path.GetFileName(e.FilePath)}"));

            var row = new ExecutionMatrixRow(
                CaseId: testCaseCode,
                Module: module,
                Scenario: scenarioName,
                TestType: run.RunType.ToString(),
                CurrentStatus: result.Status.ToString(),
                Priority: priority,
                ExpectedResult: expectedResult,
                DurationMs: result.DurationMs,
                ExecutedBy: run.TriggeredBy,
                ExecutionDate: result.ExecutedAt,
                Evidence: evidenceLinks,
                Notes: result.ErrorMessage ?? "Ejecución exitosa"
            );
            rows.Add(row);
        }

        var modules = rows.Select(r => r.Module).Distinct().OrderBy(m => m).ToList();
        var priorities = rows.Select(r => r.Priority).Distinct().OrderBy(p => p).ToList();
        var statuses = rows.Select(r => r.CurrentStatus).Distinct().OrderBy(s => s).ToList();

        return new(
            TestRunId: run.Id,
            ProjectName: projectName,
            RunType: run.RunType.ToString(),
            Environment: run.Environment.ToString(),
            RunStatus: run.Status.ToString(),
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            TotalTests: run.TotalTests,
            PassedTests: run.Passed,
            FailedTests: run.Failed,
            SkippedTests: run.Skipped,
            PassRatePercent: run.PassRatePercent,
            Rows: rows.AsReadOnly(),
            UniqueModules: modules.AsReadOnly(),
            UniquePriorities: priorities.AsReadOnly(),
            UniqueStatuses: statuses.AsReadOnly()
        );
    }

    private static byte[] GenerateExcel(ExecutionMatrixDto matrix)
    {
        using var workbook = new XLWorkbook();

        // Hoja de resumen
        var summary = workbook.Worksheets.Add("Resumen");
        summary.Cell(1, 1).Value = "Matriz de Ejecución de Pruebas";
        summary.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        summary.Cell(3, 1).Value = "Proyecto";
        summary.Cell(3, 2).Value = matrix.ProjectName;
        summary.Cell(4, 1).Value = "Tipo de ejecución";
        summary.Cell(4, 2).Value = matrix.RunType;
        summary.Cell(5, 1).Value = "Ambiente";
        summary.Cell(5, 2).Value = matrix.Environment;
        summary.Cell(6, 1).Value = "Estado";
        summary.Cell(6, 2).Value = matrix.RunStatus;
        summary.Cell(7, 1).Value = "Iniciada";
        summary.Cell(7, 2).Value = matrix.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        summary.Cell(8, 1).Value = "Completada";
        summary.Cell(8, 2).Value = matrix.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
        summary.Cell(10, 1).Value = "Total de pruebas";
        summary.Cell(10, 2).Value = matrix.TotalTests;
        summary.Cell(11, 1).Value = "Exitosas";
        summary.Cell(11, 2).Value = matrix.PassedTests;
        summary.Cell(11, 2).Style.Fill.SetBackgroundColor(XLColor.FromArgb(0, 176, 80));
        summary.Cell(12, 1).Value = "Fallidas";
        summary.Cell(12, 2).Value = matrix.FailedTests;
        summary.Cell(12, 2).Style.Fill.SetBackgroundColor(XLColor.FromArgb(255, 0, 0));
        summary.Cell(13, 1).Value = "Omitidas";
        summary.Cell(13, 2).Value = matrix.SkippedTests;
        summary.Cell(14, 1).Value = "% Éxito";
        summary.Cell(14, 2).Value = matrix.PassRatePercent;
        summary.Columns().AdjustToContents();

        // Hoja de matriz detallada con filtros
        var detail = workbook.Worksheets.Add("Ejecuciones");
        var headerRow = 1;
        detail.Cell(headerRow, 1).Value = "ID Caso";
        detail.Cell(headerRow, 2).Value = "Módulo";
        detail.Cell(headerRow, 3).Value = "Escenario / Caso de Prueba";
        detail.Cell(headerRow, 4).Value = "Tipo de Prueba";
        detail.Cell(headerRow, 5).Value = "Estado Actual";
        detail.Cell(headerRow, 6).Value = "Prioridad";
        detail.Cell(headerRow, 7).Value = "Resultado Esperado";
        detail.Cell(headerRow, 8).Value = "Duración (ms)";
        detail.Cell(headerRow, 9).Value = "Ejecutado por";
        detail.Cell(headerRow, 10).Value = "Fecha de ejecución";
        detail.Cell(headerRow, 11).Value = "Evidencia/Artefactos";
        detail.Cell(headerRow, 12).Value = "Notas / Bugs Encontrados";

        var headerRange = detail.Range(headerRow, 1, headerRow, 12);
        headerRange.Style.Font.SetBold();
        headerRange.Style.Fill.SetBackgroundColor(XLColor.FromArgb(22, 33, 62));
        headerRange.Style.Font.FontColor = XLColor.White;

        // Agregar filas de datos
        var dataRow = 2;
        foreach (var row in matrix.Rows)
        {
            detail.Cell(dataRow, 1).Value = row.CaseId;
            detail.Cell(dataRow, 2).Value = row.Module;
            detail.Cell(dataRow, 3).Value = row.Scenario;
            detail.Cell(dataRow, 4).Value = row.TestType;
            detail.Cell(dataRow, 5).Value = row.CurrentStatus;
            detail.Cell(dataRow, 6).Value = row.Priority;
            detail.Cell(dataRow, 7).Value = row.ExpectedResult;
            detail.Cell(dataRow, 8).Value = row.DurationMs;
            detail.Cell(dataRow, 9).Value = row.ExecutedBy;
            detail.Cell(dataRow, 10).Value = row.ExecutionDate.ToString("yyyy-MM-dd HH:mm:ss");
            detail.Cell(dataRow, 11).Value = row.Evidence;
            detail.Cell(dataRow, 12).Value = row.Notes;

            // Colorear estado
            var statusCell = detail.Cell(dataRow, 5);
            if (row.CurrentStatus == "Passed")
                statusCell.Style.Fill.SetBackgroundColor(XLColor.FromArgb(198, 239, 206));
            else if (row.CurrentStatus == "Failed")
                statusCell.Style.Fill.SetBackgroundColor(XLColor.FromArgb(255, 199, 206));
            else if (row.CurrentStatus == "Skipped")
                statusCell.Style.Fill.SetBackgroundColor(XLColor.FromArgb(255, 235, 205));

            dataRow++;
        }

        // Aplicar filtros autofilter
        detail.Range(1, 1, dataRow - 1, 12).CreateTable("MatrizEjecucion");
        var table = detail.Table("MatrizEjecucion");
        table.ShowAutoFilter = true;

        detail.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
