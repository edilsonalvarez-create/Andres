using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Resultado individual de una prueba dentro de una ejecución.</summary>
public class TestResult : BaseEntity
{
    private readonly List<Evidence> _evidences = [];

    private TestResult() { } // EF Core

    internal TestResult(Guid testRunId, string name, ResultStatus status, long durationMs,
        Guid? testCaseId, string? errorMessage, string? stackTrace, string? metricsJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del resultado de prueba es obligatorio.");
        TestRunId = testRunId;
        Name = name.Trim();
        Status = status;
        DurationMs = durationMs;
        TestCaseId = testCaseId;
        ErrorMessage = errorMessage;
        StackTrace = stackTrace;
        MetricsJson = metricsJson;
        ExecutedAt = DateTime.UtcNow;
    }

    public Guid TestRunId { get; private set; }
    public Guid? TestCaseId { get; private set; }
    public string Name { get; private set; } = default!;
    public ResultStatus Status { get; private set; }
    public long DurationMs { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StackTrace { get; private set; }
    /// <summary>Métricas específicas del resultado (tiempos, asserts, respuesta HTTP, etc.).</summary>
    public string? MetricsJson { get; private set; }
    public DateTime ExecutedAt { get; private set; }

    public IReadOnlyCollection<Evidence> Evidences => _evidences.AsReadOnly();

    public Evidence AttachEvidence(EvidenceType type, string filePath, string contentType, long sizeBytes)
    {
        var evidence = new Evidence(Id, type, filePath, contentType, sizeBytes);
        _evidences.Add(evidence);
        return evidence;
    }
}
