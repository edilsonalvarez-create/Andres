using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Contexto de fallo que se envía al agente de IA para diagnóstico.</summary>
public record FailureContext(
    string TestName,
    string? ErrorMessage,
    string? StackTrace,
    string? LogsExcerpt,
    string? SqlQuery,
    string? ScreenshotPath,
    TestType TestType,
    string ProjectName);

/// <summary>Diagnóstico estructurado devuelto por el agente de IA.</summary>
public record AiDiagnosisDto(
    string Diagnosis,
    string ProbableCause,
    RiskLevel Criticality,
    string Recommendation,
    DefectPriority SuggestedPriority,
    decimal EstimatedHours,
    string SuggestedOwnerRole,
    string ModelUsed);

/// <summary>Puerto: agente de IA que analiza fallos y genera diagnóstico, causa, criticidad y recomendación.</summary>
public interface IAiAnalysisService
{
    Task<AiDiagnosisDto> AnalyzeFailureAsync(FailureContext context, CancellationToken ct = default);
}

/// <summary>Puerto: agente de IA que genera casos de prueba a partir de archivos modificados en un PR.</summary>
public interface IAiTestGenerationService
{
    Task<GeneratedTestsDto> GenerateTestsForChangesAsync(
        string projectName, IReadOnlyList<string> changedFiles,
        string? diffExcerpt, CancellationToken ct = default);
}

public record GeneratedTestCase(string Title, AutomationFramework Framework, string SuggestedScript, string Rationale);
public record GeneratedTestsDto(IReadOnlyList<GeneratedTestCase> TestCases, IReadOnlyList<string> ImpactedAreas);
