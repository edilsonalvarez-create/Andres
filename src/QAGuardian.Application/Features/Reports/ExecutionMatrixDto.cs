namespace QAGuardian.Application.Features.Reports;

/// <summary>Fila de la matriz de ejecución de pruebas con trazabilidad completa.</summary>
public record ExecutionMatrixRow(
    string CaseId,
    string Module,
    string Scenario,
    string TestType,
    string CurrentStatus,
    string Priority,
    string ExpectedResult,
    long DurationMs,
    string ExecutedBy,
    DateTime ExecutionDate,
    string Evidence,
    string Notes);

/// <summary>Matriz de ejecución con datos de un TestRun específico.</summary>
public record ExecutionMatrixDto(
    Guid TestRunId,
    string ProjectName,
    string RunType,
    string Environment,
    string RunStatus,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    decimal PassRatePercent,
    IReadOnlyList<ExecutionMatrixRow> Rows,
    IReadOnlyList<string> UniqueModules,
    IReadOnlyList<string> UniquePriorities,
    IReadOnlyList<string> UniqueStatuses);
