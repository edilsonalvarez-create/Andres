using System.Text.Json;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Resultado de evaluar un quality gate sobre una ejecución de pruebas. Decide el despliegue.</summary>
public class QualityGateEvaluation : BaseEntity
{
    private readonly List<ConditionResult> _conditionResults = [];

    private QualityGateEvaluation() { } // EF Core

    public QualityGateEvaluation(Guid testRunId, Guid qualityGateId)
    {
        TestRunId = testRunId;
        QualityGateId = qualityGateId;
        Status = QualityGateStatus.Passed;
        EvaluatedAt = DateTime.UtcNow;
    }

    public Guid TestRunId { get; private set; }
    public Guid QualityGateId { get; private set; }
    public QualityGateStatus Status { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    /// <summary>Detalle serializado de cada condición evaluada.</summary>
    public string DetailsJson { get; private set; } = "[]";

    public bool DeploymentApproved => Status != QualityGateStatus.Failed;

    public record ConditionResult(
        GateMetric Metric,
        GateOperator Operator,
        decimal Threshold,
        decimal? ActualValue,
        bool Satisfied,
        bool IsBlocking);

    public IReadOnlyList<ConditionResult> GetConditionResults() =>
        _conditionResults.Count > 0
            ? _conditionResults.AsReadOnly()
            : JsonSerializer.Deserialize<List<ConditionResult>>(DetailsJson) ?? [];

    internal void AddConditionResult(GateMetric metric, GateOperator op, decimal threshold,
        decimal? actual, bool satisfied, bool isBlocking)
        => _conditionResults.Add(new ConditionResult(metric, op, threshold, actual, satisfied, isBlocking));

    internal void Finalize_()
    {
        if (_conditionResults.Any(c => !c.Satisfied && c.IsBlocking))
            Status = QualityGateStatus.Failed;
        else if (_conditionResults.Any(c => !c.Satisfied))
            Status = QualityGateStatus.Warning;
        else
            Status = QualityGateStatus.Passed;

        DetailsJson = JsonSerializer.Serialize(_conditionResults);
    }
}
