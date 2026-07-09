using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Dashboard;
using QAGuardian.Infrastructure.Reports;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class DashboardReportTests
{
    private static DashboardDto Sample() => new(
        TotalProjects: 3, TotalTestCases: 120, AutomatedTestCases: 90,
        AutomationCoveragePercent: 75m, RunsLast30Days: 40, TestsExecuted: 500,
        TestsPassed: 470, TestsFailed: 30, TestsPending: 2, PassRatePercent: 94m,
        AvgRunDurationSeconds: 12.5, OpenDefects: 8, CriticalDefectsOpen: 1,
        VulnerabilitiesHighOrCritical: 0, QualityScore: 89m, AvailabilityPercent: 97.5m,
        ErrorsByModule: [new ModuleErrorStat("Autenticación", 12), new ModuleErrorStat("Pagos", 5)],
        Heatmap: [new HeatmapCell("Autenticación", "2026-07-01", 3)],
        Trend: []);

    private static RunReportGenerator CreateGenerator()
        => new(Substitute.For<ITestRunRepository>(), Substitute.For<IProjectRepository>());

    [Fact]
    public void Exporta_dashboard_en_excel()
    {
        var (content, contentType, fileName) = CreateGenerator()
            .GenerateDashboardReport(Sample(), "Dashboard", ReportFormat.Excel);

        content.Should().NotBeEmpty();
        contentType.Should().Contain("spreadsheetml");
        fileName.Should().EndWith(".xlsx");
    }

    [Fact]
    public void Exporta_dashboard_en_pdf()
    {
        var (content, contentType, fileName) = CreateGenerator()
            .GenerateDashboardReport(Sample(), "Dashboard", ReportFormat.Pdf);

        content.Should().NotBeEmpty();
        // Los PDF comienzan con la firma "%PDF".
        System.Text.Encoding.ASCII.GetString(content, 0, 4).Should().Be("%PDF");
        contentType.Should().Be("application/pdf");
        fileName.Should().EndWith(".pdf");
    }

    [Fact]
    public void Formato_no_soportado_para_dashboard_lanza_excepcion()
    {
        var act = () => CreateGenerator().GenerateDashboardReport(Sample(), "Dashboard", ReportFormat.Csv);
        act.Should().Throw<NotSupportedException>();
    }
}
