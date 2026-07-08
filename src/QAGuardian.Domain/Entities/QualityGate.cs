using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Agregado raíz: quality gate con condiciones que aprueban o rechazan un despliegue.</summary>
public class QualityGate : AuditableEntity
{
    private readonly List<QualityGateCondition> _conditions = [];

    private QualityGate() { } // EF Core

    public QualityGate(string name, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del quality gate es obligatorio.");
        Name = name.Trim();
        IsDefault = isDefault;
    }

    public string Name { get; private set; } = default!;
    public bool IsDefault { get; private set; }

    public IReadOnlyCollection<QualityGateCondition> Conditions => _conditions.AsReadOnly();

    public QualityGateCondition AddCondition(GateMetric metric, GateOperator op, decimal threshold, bool isBlocking = true)
    {
        if (_conditions.Any(c => c.Metric == metric))
            throw new DomainException($"Ya existe una condición para la métrica {metric}.");
        var condition = new QualityGateCondition(Id, metric, op, threshold, isBlocking);
        _conditions.Add(condition);
        return condition;
    }

    public void RemoveCondition(Guid conditionId)
    {
        var condition = _conditions.FirstOrDefault(c => c.Id == conditionId)
            ?? throw new NotFoundException(nameof(QualityGateCondition), conditionId);
        _conditions.Remove(condition);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del quality gate es obligatorio.");
        Name = name.Trim();
    }

    public void MarkAsDefault() => IsDefault = true;
    public void UnmarkDefault() => IsDefault = false;

    /// <summary>Evalúa las métricas recibidas contra las condiciones del gate.</summary>
    public QualityGateEvaluation Evaluate(Guid testRunId, IReadOnlyDictionary<GateMetric, decimal> metrics)
    {
        var evaluation = new QualityGateEvaluation(testRunId, Id);
        foreach (var condition in _conditions)
        {
            var hasValue = metrics.TryGetValue(condition.Metric, out var actual);
            var satisfied = hasValue && condition.IsSatisfiedBy(actual);
            evaluation.AddConditionResult(condition.Metric, condition.Operator, condition.Threshold,
                hasValue ? actual : null, satisfied, condition.IsBlocking);
        }
        evaluation.Finalize_();
        return evaluation;
    }
}
