using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using QAGuardian.Infrastructure.Reports;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class PowerPointBuilderTests
{
    [Fact]
    public void Genera_un_pptx_valido_con_titulo_y_secciones()
    {
        var bytes = PowerPointBuilder.Build(
            "QA Guardian — Demo",
            "Reporte de ejecución",
            [
                new PptxSection("Resumen ejecutivo", ["Total: 10", "Exitosas: 9", "% de éxito: 90%"]),
                new PptxSection("Resultados", ["[Passed] Login (120 ms)", "[Failed] Pago (300 ms) — timeout"])
            ]);

        bytes.Should().NotBeEmpty();
        // Un .pptx es un paquete ZIP: firma "PK".
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');

        // El paquete se puede reabrir y contiene título + 2 secciones = 3 slides.
        using var stream = new MemoryStream(bytes);
        using var document = PresentationDocument.Open(stream, isEditable: false);
        document.PresentationPart.Should().NotBeNull();
        document.PresentationPart!.SlideParts.Count().Should().Be(3);
    }

    [Fact]
    public void Sin_secciones_genera_solo_el_slide_de_titulo()
    {
        var bytes = PowerPointBuilder.Build("Título", "Subtítulo", []);

        using var stream = new MemoryStream(bytes);
        using var document = PresentationDocument.Open(stream, isEditable: false);
        document.PresentationPart!.SlideParts.Count().Should().Be(1);
    }
}
