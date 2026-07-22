using System.Text;
using FluentAssertions;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Reports;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// Cubre los 8 formatos del escritor de reportes de ejecución (extraído en Sprint 4). Verifica
/// firmas de archivo reales (magic bytes) y presencia de datos clave — no solo "no vacío".
/// </summary>
public class RunReportWriterTests
{
    private readonly RunReportWriter _writer = new();

    static RunReportWriterTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static TestRun SampleRun()
    {
        var run = new TestRun(Guid.NewGuid(), TestType.Regression, EnvironmentType.QA, "ci@qaguardian");
        run.Start();
        run.AddResult("Login válido", ResultStatus.Passed, 120);
        run.AddResult("Pago con tarjeta", ResultStatus.Failed, 340, errorMessage: "NullReference en checkout");
        run.AddResult("Logout", ResultStatus.Skipped, 0);
        run.Complete();
        return run;
    }

    [Fact]
    public void Pdf_tiene_firma_valida()
    {
        var bytes = _writer.Write(SampleRun(), "Proyecto ERP", ReportFormat.Pdf);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void Excel_y_Word_y_PowerPoint_son_paquetes_OpenXml_ZIP()
    {
        // OOXML (xlsx/docx/pptx) son archivos ZIP: comienzan con "PK".
        foreach (var format in new[] { ReportFormat.Excel, ReportFormat.Word, ReportFormat.PowerPoint })
        {
            var bytes = _writer.Write(SampleRun(), "Proyecto ERP", format);
            Encoding.ASCII.GetString(bytes, 0, 2).Should().Be("PK", $"formato {format} debe ser OOXML/ZIP");
        }
    }

    [Fact]
    public void Json_incluye_los_conteos_y_el_error()
    {
        var json = Encoding.UTF8.GetString(_writer.Write(SampleRun(), "Proyecto ERP", ReportFormat.Json));
        json.Should().Contain("\"total\": 3");
        json.Should().Contain("\"fallidas\": 1");
        json.Should().Contain("NullReference en checkout");
    }

    [Fact]
    public void Csv_escapa_comillas_y_lista_cada_resultado()
    {
        var csv = Encoding.UTF8.GetString(_writer.Write(SampleRun(), "Proyecto ERP", ReportFormat.Csv));
        csv.Should().StartWith("Prueba,Estado,DuracionMs,Error");
        csv.Should().Contain("Login válido");
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(4); // encabezado + 3 filas
    }

    [Fact]
    public void Html_escapa_contenido_y_marca_fallos()
    {
        var html = Encoding.UTF8.GetString(_writer.Write(SampleRun(), "Proyecto <ERP>", ReportFormat.Html));
        html.Should().Contain("Proyecto &lt;ERP&gt;"); // nombre escapado (anti-XSS)
        html.Should().Contain("class='fail'");
    }

    [Fact]
    public void Xml_es_bien_formado_y_tiene_los_resultados()
    {
        var bytes = _writer.Write(SampleRun(), "Proyecto ERP", ReportFormat.Xml);
        var doc = System.Xml.Linq.XDocument.Load(new MemoryStream(bytes));
        doc.Root!.Name.LocalName.Should().Be("TestRun");
        doc.Descendants("Result").Should().HaveCount(3);
    }
}
