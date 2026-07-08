using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Contexto de ejecución que recibe cada runner.</summary>
public record TestRunContext(
    Guid TestRunId,
    Guid ProjectId,
    TestType RunType,
    EnvironmentType Environment,
    string WorkingDirectory,
    IReadOnlyList<TestScriptRef> Scripts,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>Referencia a un script automatizado a ejecutar.</summary>
public record TestScriptRef(Guid? TestCaseId, string Name, string ScriptPath);

/// <summary>Evidencia producida por un runner.</summary>
public record RunnerEvidence(EvidenceType Type, string FilePath, string ContentType, long SizeBytes);

/// <summary>Resultado individual reportado por un runner.</summary>
public record RunnerResultItem(
    Guid? TestCaseId,
    string Name,
    ResultStatus Status,
    long DurationMs,
    string? ErrorMessage,
    string? StackTrace,
    string? MetricsJson,
    IReadOnlyList<RunnerEvidence> Evidences);

/// <summary>Hallazgo de seguridad reportado por un runner (ZAP).</summary>
public record RunnerSecurityFinding(
    string Name, RiskLevel Risk, string Category, string? Url,
    string? Parameter, string? Evidence, string? Solution, string? CweId);

/// <summary>Salida agregada de la ejecución de un runner.</summary>
public record RunnerOutcome(
    bool Succeeded,
    IReadOnlyList<RunnerResultItem> Results,
    IReadOnlyList<RunnerSecurityFinding> SecurityFindings,
    string? AggregateMetricsJson,
    string? ErrorMessage);

/// <summary>Puerto: motor de ejecución de pruebas (Playwright, Newman, JMeter, ZAP, SQL).</summary>
public interface ITestRunner
{
    AutomationFramework Framework { get; }
    Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default);
}

/// <summary>Resuelve el runner adecuado según el framework de automatización.</summary>
public interface ITestRunnerFactory
{
    ITestRunner Resolve(AutomationFramework framework);
    ITestRunner ResolveByTestType(TestType testType);
}
