using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Agregado raíz: ejecución de un conjunto de pruebas (funcional, regresión, API, rendimiento, seguridad, etc.).</summary>
public class TestRun : AuditableEntity
{
    private readonly List<TestResult> _results = [];

    private TestRun() { } // EF Core

    public TestRun(
        Guid projectId,
        TestType runType,
        EnvironmentType environment,
        string triggeredBy,
        Guid? versionId = null,
        string? commitSha = null,
        int? pullRequestNumber = null)
    {
        ProjectId = projectId;
        RunType = runType;
        Environment = environment;
        TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "system" : triggeredBy;
        VersionId = versionId;
        CommitSha = commitSha;
        PullRequestNumber = pullRequestNumber;
        Status = RunStatus.Pending;
    }

    public Guid ProjectId { get; private set; }
    public Guid? VersionId { get; private set; }
    public TestType RunType { get; private set; }
    public EnvironmentType Environment { get; private set; }
    public RunStatus Status { get; private set; }
    public string TriggeredBy { get; private set; } = default!;
    public string? CommitSha { get; private set; }
    public int? PullRequestNumber { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    /// <summary>Métricas agregadas serializadas (TPS, memoria, CPU, etc.) para runs de rendimiento.</summary>
    public string? MetricsJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    public QualityGateEvaluation? GateEvaluation { get; private set; }
    public IReadOnlyCollection<TestResult> Results => _results.AsReadOnly();

    public int TotalTests => _results.Count;
    public int Passed => _results.Count(r => r.Status == ResultStatus.Passed);
    public int Failed => _results.Count(r => r.Status == ResultStatus.Failed);
    public int Skipped => _results.Count(r => r.Status == ResultStatus.Skipped);
    public decimal PassRatePercent => TotalTests == 0 ? 0 : Math.Round(Passed * 100m / TotalTests, 2);
    public double? DurationSeconds => StartedAt.HasValue && CompletedAt.HasValue
        ? (CompletedAt.Value - StartedAt.Value).TotalSeconds
        : null;

    public void Start()
    {
        if (Status != RunStatus.Pending)
            throw new DomainException("Solo se puede iniciar una ejecución en estado Pendiente.");
        Status = RunStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public TestResult AddResult(string name, ResultStatus status, long durationMs,
        Guid? testCaseId = null, string? errorMessage = null, string? stackTrace = null, string? metricsJson = null)
    {
        if (Status != RunStatus.Running)
            throw new DomainException("Solo se pueden agregar resultados a una ejecución en curso.");
        var result = new TestResult(Id, name, status, durationMs, testCaseId, errorMessage, stackTrace, metricsJson);
        _results.Add(result);
        return result;
    }

    public void Complete(string? metricsJson = null)
    {
        if (Status != RunStatus.Running)
            throw new DomainException("Solo se puede completar una ejecución en curso.");
        Status = RunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        MetricsJson = metricsJson ?? MetricsJson;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = RunStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void Cancel()
    {
        if (Status is RunStatus.Completed or RunStatus.Failed)
            throw new DomainException("No se puede cancelar una ejecución finalizada.");
        Status = RunStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void AttachGateEvaluation(QualityGateEvaluation evaluation) => GateEvaluation = evaluation;
}
