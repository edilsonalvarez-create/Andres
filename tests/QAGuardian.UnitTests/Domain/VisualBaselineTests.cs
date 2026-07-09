using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class VisualBaselineTests
{
    private static VisualBaseline New() =>
        new(Guid.NewGuid(), "tc-0001", "baselines/x/tc-0001.png", 1280, 720, 0.10m, 30);

    [Fact]
    public void Crea_baseline_con_valores_validos()
    {
        var baseline = New();
        baseline.BaselineKey.Should().Be("tc-0001");
        baseline.ThresholdPercent.Should().Be(0.10m);
        baseline.PixelTolerance.Should().Be(30);
    }

    [Fact]
    public void Rechaza_clave_vacia()
    {
        var act = () => new VisualBaseline(Guid.NewGuid(), "", "ruta.png", 100, 100, 0.1m, 30);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Rechaza_umbral_fuera_de_rango(decimal threshold)
    {
        var baseline = New();
        var act = () => baseline.SetThreshold(threshold, 30);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Rechaza_tolerancia_de_pixel_fuera_de_rango()
    {
        var baseline = New();
        var act = () => baseline.SetThreshold(0.1m, 800);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Actualizar_baseline_cambia_ruta_y_dimensiones()
    {
        var baseline = New();
        baseline.UpdateBaseline("baselines/x/tc-0001-v2.png", 1920, 1080);
        baseline.BaselinePath.Should().Be("baselines/x/tc-0001-v2.png");
        baseline.Width.Should().Be(1920);
        baseline.Height.Should().Be(1080);
    }
}
