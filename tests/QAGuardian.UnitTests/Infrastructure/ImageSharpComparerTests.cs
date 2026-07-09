using FluentAssertions;
using QAGuardian.Infrastructure.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class ImageSharpComparerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"visual-tests-{Guid.NewGuid():N}");
    private readonly ImageSharpComparer _comparer = new();

    public ImageSharpComparerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* mejor esfuerzo */ }
    }

    private string SaveSolid(string name, int width, int height, Rgba32 color)
    {
        var path = Path.Combine(_dir, name);
        using var img = new Image<Rgba32>(width, height, color);
        img.SaveAsPng(path);
        return path;
    }

    [Fact]
    public void Imagenes_identicas_dan_0_por_ciento_de_diferencia()
    {
        var white = new Rgba32(255, 255, 255);
        var baseline = SaveSolid("base.png", 20, 20, white);
        var actual = SaveSolid("actual.png", 20, 20, white);
        var diff = Path.Combine(_dir, "diff.png");

        var result = _comparer.Compare(baseline, actual, diff, pixelTolerance: 30);

        result.SizeMatched.Should().BeTrue();
        result.MismatchPercent.Should().Be(0);
        result.DifferentPixels.Should().Be(0);
        result.TotalPixels.Should().Be(400);
        File.Exists(diff).Should().BeTrue();
    }

    [Fact]
    public void Imagenes_totalmente_distintas_dan_100_por_ciento()
    {
        var baseline = SaveSolid("base.png", 10, 10, new Rgba32(0, 0, 0));
        var actual = SaveSolid("actual.png", 10, 10, new Rgba32(255, 255, 255));
        var diff = Path.Combine(_dir, "diff.png");

        var result = _comparer.Compare(baseline, actual, diff, pixelTolerance: 30);

        result.SizeMatched.Should().BeTrue();
        result.MismatchPercent.Should().Be(100);
        result.DifferentPixels.Should().Be(100);
    }

    [Fact]
    public void Dimensiones_distintas_se_reportan_como_cambio_total()
    {
        var baseline = SaveSolid("base.png", 10, 10, new Rgba32(255, 255, 255));
        var actual = SaveSolid("actual.png", 20, 20, new Rgba32(255, 255, 255));
        var diff = Path.Combine(_dir, "diff.png");

        var result = _comparer.Compare(baseline, actual, diff, pixelTolerance: 30);

        result.SizeMatched.Should().BeFalse();
        result.MismatchPercent.Should().Be(100);
    }

    [Fact]
    public void Diferencia_dentro_de_la_tolerancia_no_cuenta_como_distinta()
    {
        // ΔR+ΔG+ΔB = 3+3+3 = 9, por debajo de la tolerancia 30.
        var baseline = SaveSolid("base.png", 10, 10, new Rgba32(100, 100, 100));
        var actual = SaveSolid("actual.png", 10, 10, new Rgba32(103, 103, 103));
        var diff = Path.Combine(_dir, "diff.png");

        var result = _comparer.Compare(baseline, actual, diff, pixelTolerance: 30);

        result.DifferentPixels.Should().Be(0);
        result.MismatchPercent.Should().Be(0);
    }

    [Fact]
    public void Una_region_modificada_produce_porcentaje_parcial()
    {
        var baseline = SaveSolid("base.png", 10, 10, new Rgba32(255, 255, 255));
        // Pintar una fila (10 píxeles de 100) de negro en la imagen actual.
        var actualPath = Path.Combine(_dir, "actual.png");
        using (var img = new Image<Rgba32>(10, 10, new Rgba32(255, 255, 255)))
        {
            for (var x = 0; x < 10; x++) img[x, 0] = new Rgba32(0, 0, 0);
            img.SaveAsPng(actualPath);
        }
        var diff = Path.Combine(_dir, "diff.png");

        var result = _comparer.Compare(baseline, actualPath, diff, pixelTolerance: 30);

        result.DifferentPixels.Should().Be(10);
        result.MismatchPercent.Should().Be(10);
    }
}
