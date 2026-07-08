using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Diagnóstico generado por el agente de IA sobre un fallo de prueba o defecto.</summary>
public class AiAnalysis : BaseEntity
{
    private AiAnalysis() { } // EF Core

    public AiAnalysis(
        Guid? testResultId,
        Guid? defectId,
        string diagnosis,
        string probableCause,
        RiskLevel criticality,
        string recommendation,
        DefectPriority suggestedPriority,
        decimal estimatedHours,
        string suggestedOwnerRole,
        string modelUsed)
    {
        if (testResultId is null && defectId is null)
            throw new DomainException("El análisis debe asociarse a un resultado de prueba o a un defecto.");
        TestResultId = testResultId;
        DefectId = defectId;
        Diagnosis = diagnosis;
        ProbableCause = probableCause;
        Criticality = criticality;
        Recommendation = recommendation;
        SuggestedPriority = suggestedPriority;
        EstimatedHours = estimatedHours;
        SuggestedOwnerRole = suggestedOwnerRole;
        ModelUsed = modelUsed;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid? TestResultId { get; private set; }
    public Guid? DefectId { get; private set; }
    public string Diagnosis { get; private set; } = default!;
    public string ProbableCause { get; private set; } = default!;
    public RiskLevel Criticality { get; private set; }
    public string Recommendation { get; private set; } = default!;
    public DefectPriority SuggestedPriority { get; private set; }
    public decimal EstimatedHours { get; private set; }
    public string SuggestedOwnerRole { get; private set; } = default!;
    public string ModelUsed { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
}
