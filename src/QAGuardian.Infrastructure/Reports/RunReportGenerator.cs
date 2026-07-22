using QuestPDF.Infrastructure;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Dashboard;
using QAGuardian.Application.Features.Reports;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Reports;

/// <summary>
/// Orquesta la generación de los 3 tipos de reporte de la plataforma (ejecución, dashboard,
/// matriz de ejecución), delegando el contenido binario a un escritor especializado por
/// familia (<see cref="RunReportWriter"/>, <see cref="DashboardReportWriter"/>,
/// <see cref="ExecutionMatrixReportWriter"/>).
///
/// Antes del Sprint 4 esta clase concentraba las 3 familias en un solo archivo de 618 líneas
/// — violación de SRP documentada en ADR-008. Esta clase resuelve únicamente: identidad del
/// recurso (buscar el TestRun/proyecto), nombre de archivo y content-type; el "cómo se ve" cada
/// formato vive en su escritor.
/// </summary>
public class RunReportGenerator : IReportGenerator
{
    private readonly ITestRunRepository _runs;
    private readonly IProjectRepository _projects;
    private readonly RunReportWriter _runReportWriter = new();
    private readonly DashboardReportWriter _dashboardWriter = new();
    private readonly ExecutionMatrixReportWriter _matrixWriter;

    static RunReportGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public RunReportGenerator(ITestRunRepository runs, IProjectRepository projects)
    {
        _runs = runs;
        _projects = projects;
        _matrixWriter = new ExecutionMatrixReportWriter(runs, projects);
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
            ReportFormat.Json => (_runReportWriter.Write(run, projectName, format), "application/json", $"{baseName}.json"),
            ReportFormat.Csv => (_runReportWriter.Write(run, projectName, format), "text/csv", $"{baseName}.csv"),
            ReportFormat.Html => (_runReportWriter.Write(run, projectName, format), "text/html", $"{baseName}.html"),
            ReportFormat.Excel => (_runReportWriter.Write(run, projectName, format),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx"),
            ReportFormat.Pdf => (_runReportWriter.Write(run, projectName, format), "application/pdf", $"{baseName}.pdf"),
            ReportFormat.Word => (_runReportWriter.Write(run, projectName, format),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{baseName}.docx"),
            ReportFormat.Xml => (_runReportWriter.Write(run, projectName, format), "application/xml", $"{baseName}.xml"),
            ReportFormat.PowerPoint => (_runReportWriter.Write(run, projectName, format),
                "application/vnd.openxmlformats-officedocument.presentationml.presentation", $"{baseName}.pptx"),
            _ => throw new NotSupportedException($"Formato no soportado: {format}")
        };
    }

    public (byte[] Content, string ContentType, string FileName) GenerateDashboardReport(
        DashboardDto stats, string title, ReportFormat format)
    {
        var baseName = $"dashboard-{DateTime.UtcNow:yyyyMMdd-HHmm}";
        return format switch
        {
            ReportFormat.Excel => (_dashboardWriter.Write(stats, title, format),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{baseName}.xlsx"),
            ReportFormat.Pdf => (_dashboardWriter.Write(stats, title, format), "application/pdf", $"{baseName}.pdf"),
            _ => throw new NotSupportedException($"El dashboard solo se exporta en PDF o Excel (recibido: {format}).")
        };
    }

    public Task<ExecutionMatrixDto> GetExecutionMatrixAsync(Guid testRunId, CancellationToken ct = default)
        => _matrixWriter.GetMatrixAsync(testRunId, ct);

    public Task<(byte[] Content, string ContentType, string FileName)> GenerateExecutionMatrixReportAsync(
        Guid testRunId, CancellationToken ct = default)
        => _matrixWriter.GenerateExcelReportAsync(testRunId, ct);
}
