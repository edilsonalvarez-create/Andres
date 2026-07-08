using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Condición individual de un quality gate.</summary>
public class QualityGateCondition : BaseEntity
{
    private QualityGateCondition() { } // EF Core

    public QualityGateCondition(Guid qualityGateId, GateMetric metric, GateOperator op, decimal threshold, bool isBlocking)
    {
        QualityGateId = qualityGateId;
        Metric = metric;
        Operator = op;
        Threshold = threshold;
        IsBlocking = isBlocking;
    }

    public Guid QualityGateId { get; private set; }
    public GateMetric Metric { get; private set; }
    public GateOperator Operator { get; private set; }
    public decimal Threshold { get; private set; }
    /// <summary>Si es true, el incumplimiento rechaza el despliegue; si es false, solo genera advertencia.</summary>
    public bool IsBlocking { get; private set; }

    public bool IsSatisfiedBy(decimal actual) => Operator switch
    {
        GateOperator.GreaterOrEqual => actual >= Threshold,
        GateOperator.LessOrEqual => actual <= Threshold,
        GateOperator.Equal => actual == Threshold,
        _ => throw new DomainException($"Operador de gate no soportado: {Operator}")
    };
}
