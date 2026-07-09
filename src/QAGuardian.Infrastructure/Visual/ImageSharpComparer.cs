using QAGuardian.Application.Abstractions.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace QAGuardian.Infrastructure.Visual;

/// <summary>
/// Comparador de imágenes basado en ImageSharp (multiplataforma, funciona en Linux/Docker).
/// Cuenta los píxeles cuya diferencia de color (|ΔR|+|ΔG|+|ΔB|) supera la tolerancia y
/// genera una imagen de diferencias resaltando esos píxeles en rojo.
/// </summary>
public class ImageSharpComparer : IImageComparer
{
    private static readonly Rgba32 DiffColor = new(255, 0, 0, 255);

    public ImageComparisonResult Compare(string baselinePath, string actualPath, string diffOutputPath, int pixelTolerance)
    {
        using var baseline = Image.Load<Rgba32>(baselinePath);
        using var actual = Image.Load<Rgba32>(actualPath);

        // Dimensiones distintas = cambio visual total; se genera un diff con la imagen actual marcada.
        if (baseline.Width != actual.Width || baseline.Height != actual.Height)
        {
            using var mismatch = actual.Clone();
            mismatch.SaveAsPng(diffOutputPath);
            var total = actual.Width * actual.Height;
            return new ImageComparisonResult(false, 100d, total, total);
        }

        var width = baseline.Width;
        var height = baseline.Height;
        var totalPixels = width * height;
        var differentPixels = 0;

        using var diff = actual.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var b = baseline[x, y];
                var a = actual[x, y];
                var delta = Math.Abs(b.R - a.R) + Math.Abs(b.G - a.G) + Math.Abs(b.B - a.B);
                if (delta > pixelTolerance)
                {
                    differentPixels++;
                    diff[x, y] = DiffColor;
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(diffOutputPath)!);
        diff.SaveAsPng(diffOutputPath);

        var mismatchPercent = totalPixels == 0 ? 0d : Math.Round(differentPixels * 100d / totalPixels, 4);
        return new ImageComparisonResult(true, mismatchPercent, differentPixels, totalPixels);
    }
}
