using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Evidencia generada automáticamente (screenshot, video, log, reporte).</summary>
public class Evidence : BaseEntity
{
    private Evidence() { } // EF Core

    public Evidence(Guid testResultId, EvidenceType type, string filePath, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new DomainException("La ruta del archivo de evidencia es obligatoria.");
        TestResultId = testResultId;
        Type = type;
        FilePath = filePath.Trim();
        ContentType = contentType;
        SizeBytes = sizeBytes;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid TestResultId { get; private set; }
    public EvidenceType Type { get; private set; }
    public string FilePath { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
