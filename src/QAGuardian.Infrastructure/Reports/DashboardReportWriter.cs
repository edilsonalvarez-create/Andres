using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Dashboard;

namespace QAGuardian.Infrastructure.Reports;

/// <summary>
/// Genera el contenido binario del reporte del dashboard ejecutivo (PDF/Excel). Extraído de
/// <see cref="RunReportGenerator"/> (Sprint 4, Extract Class) — ver ADR-008.
/// </summary>
public class DashboardReportWriter
{
    public byte[] Write(DashboardDto stats, string title, ReportFormat format) => format switch
    {
        ReportFormat.Excel => GenerateExcel(stats, title),
        ReportFormat.Pdf => GeneratePdf(stats, title),
        _ => throw new NotSupportedException($"El dashboard solo se exporta en PDF o Excel (recibido: {format}).")
    };

    private static (string Label, object Value)[] Kpis(DashboardDto s) =>
    [
        ("Proyectos activos", s.TotalProjects),
        ("Casos de prueba", s.TotalTestCases),
        ("Automatizados", s.AutomatedTestCases),
        ("Cobertura de automatización (%)", s.AutomationCoveragePercent),
        ("Ejecuciones (30 días)", s.RunsLast30Days),
        ("Pruebas ejecutadas", s.TestsExecuted),
        ("Exitosas", s.TestsPassed),
        ("Fallidas", s.TestsFailed),
        ("Pendientes", s.TestsPending),
        ("% de éxito", s.PassRatePercent),
        ("Tiempo promedio (s)", Math.Round(s.AvgRunDurationSeconds, 2)),
        ("Defectos abiertos", s.OpenDefects),
        ("Defectos críticos", s.CriticalDefectsOpen),
        ("Vulnerabilidades altas/críticas", s.VulnerabilitiesHighOrCritical),
        ("Disponibilidad (%)", s.AvailabilityPercent),
        ("Índice de calidad", s.QualityScore)
    ];

    private static byte[] GenerateExcel(DashboardDto stats, string title)
    {
        using var workbook = new XLWorkbook();
        var kpis = workbook.Worksheets.Add("KPIs");
        kpis.Cell(1, 1).Value = title;
        kpis.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 14;
        var row = 3;
        foreach (var (label, value) in Kpis(stats))
        {
            kpis.Cell(row, 1).Value = label;
            kpis.Cell(row, 2).Value = value.ToString();
            row++;
        }
        kpis.Columns().AdjustToContents();

        var modules = workbook.Worksheets.Add("Errores por módulo");
        modules.Cell(1, 1).Value = "Módulo";
        modules.Cell(1, 2).Value = "Fallos";
        modules.Row(1).Style.Font.SetBold();
        var mrow = 2;
        foreach (var m in stats.ErrorsByModule)
        {
            modules.Cell(mrow, 1).Value = m.ModuleName;
            modules.Cell(mrow, 2).Value = m.FailedCount;
            mrow++;
        }
        modules.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] GeneratePdf(DashboardDto stats, string title)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Text(title).FontSize(18).Bold().FontColor(Colors.Indigo.Darken3);
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Indicadores ejecutivos").Bold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });
                        foreach (var (label, value) in Kpis(stats))
                        {
                            table.Cell().Padding(3).Text(label).FontSize(10);
                            table.Cell().Padding(3).AlignRight().Text(value.ToString()!).FontSize(10).Bold();
                        }
                    });

                    if (stats.ErrorsByModule.Count > 0)
                    {
                        col.Item().PaddingTop(10).Text("Errores por módulo").Bold().FontSize(13);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.RelativeColumn(3); columns.RelativeColumn(1); });
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Indigo.Darken3).Padding(4).Text("Módulo").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Indigo.Darken3).Padding(4).Text("Fallos").FontColor(Colors.White).Bold();
                            });
                            foreach (var m in stats.ErrorsByModule)
                            {
                                table.Cell().Padding(3).Text(m.ModuleName).FontSize(10);
                                table.Cell().Padding(3).AlignRight().Text(m.FailedCount.ToString()).FontSize(10);
                            }
                        });
                    }
                });
                page.Footer().AlignCenter()
                    .Text($"Generado por QA Guardian — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });
        });
        return document.GeneratePdf();
    }
}
