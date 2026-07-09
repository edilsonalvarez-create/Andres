namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Resultado de comparar dos imágenes píxel a píxel.</summary>
/// <param name="SizeMatched">Falso si las dimensiones difieren (se considera 100 % de cambio).</param>
/// <param name="MismatchPercent">Porcentaje de píxeles distintos respecto al total.</param>
/// <param name="DifferentPixels">Cantidad de píxeles que superaron la tolerancia.</param>
/// <param name="TotalPixels">Total de píxeles comparados.</param>
public record ImageComparisonResult(
    bool SizeMatched,
    double MismatchPercent,
    int DifferentPixels,
    int TotalPixels);

/// <summary>Puerto: comparador de imágenes para regresión visual.</summary>
public interface IImageComparer
{
    /// <summary>
    /// Compara <paramref name="baselinePath"/> contra <paramref name="actualPath"/>, escribe
    /// una imagen de diferencias en <paramref name="diffOutputPath"/> (píxeles distintos en rojo)
    /// y devuelve las métricas de la comparación.
    /// </summary>
    ImageComparisonResult Compare(string baselinePath, string actualPath, string diffOutputPath, int pixelTolerance);
}
