using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>
/// Imagen de referencia (baseline) para regresión visual, identificada por
/// proyecto + clave estable (id del caso de prueba o nombre del escenario).
/// </summary>
public class VisualBaseline : AuditableEntity
{
    private VisualBaseline() { } // EF Core

    public VisualBaseline(Guid projectId, string baselineKey, string baselinePath,
        int width, int height, decimal thresholdPercent, int pixelTolerance)
    {
        if (string.IsNullOrWhiteSpace(baselineKey))
            throw new DomainException("La clave del baseline es obligatoria.");
        if (string.IsNullOrWhiteSpace(baselinePath))
            throw new DomainException("La ruta del baseline es obligatoria.");
        ProjectId = projectId;
        BaselineKey = baselineKey;
        BaselinePath = baselinePath;
        Width = width;
        Height = height;
        SetThreshold(thresholdPercent, pixelTolerance);
    }

    public Guid ProjectId { get; private set; }
    /// <summary>Clave estable: id del caso de prueba o nombre del escenario visual.</summary>
    public string BaselineKey { get; private set; } = default!;
    /// <summary>Ruta relativa de la imagen de referencia en el almacenamiento de evidencias.</summary>
    public string BaselinePath { get; private set; } = default!;
    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>Porcentaje máximo de píxeles distintos tolerado (p. ej. 0.10 = 0,10 %).</summary>
    public decimal ThresholdPercent { get; private set; }
    /// <summary>Diferencia de color por píxel (suma |ΔR|+|ΔG|+|ΔB|) a partir de la cual cuenta como distinto.</summary>
    public int PixelTolerance { get; private set; }

    public void SetThreshold(decimal thresholdPercent, int pixelTolerance)
    {
        if (thresholdPercent < 0 || thresholdPercent > 100)
            throw new DomainException("El umbral debe estar entre 0 y 100 por ciento.");
        if (pixelTolerance < 0 || pixelTolerance > 765)
            throw new DomainException("La tolerancia de píxel debe estar entre 0 y 765.");
        ThresholdPercent = thresholdPercent;
        PixelTolerance = pixelTolerance;
    }

    /// <summary>Reemplaza la imagen de referencia (re-baseline aprobado por QA).</summary>
    public void UpdateBaseline(string baselinePath, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(baselinePath))
            throw new DomainException("La ruta del baseline es obligatoria.");
        BaselinePath = baselinePath;
        Width = width;
        Height = height;
    }
}
