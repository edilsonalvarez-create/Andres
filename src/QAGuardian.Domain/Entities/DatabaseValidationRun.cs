using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Comparación de esquemas SQL Server entre dos ambientes (tablas, índices, llaves, SPs, triggers).</summary>
public class DatabaseValidationRun : AuditableEntity
{
    private DatabaseValidationRun() { } // EF Core

    public DatabaseValidationRun(Guid projectId, string sourceEnvironment, string targetEnvironment)
    {
        ProjectId = projectId;
        SourceEnvironment = sourceEnvironment;
        TargetEnvironment = targetEnvironment;
        Status = RunStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public Guid ProjectId { get; private set; }
    public string SourceEnvironment { get; private set; } = default!;
    public string TargetEnvironment { get; private set; } = default!;
    public RunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int DifferencesCount { get; private set; }
    /// <summary>Diferencias detectadas serializadas por categoría (tablas, columnas, índices, llaves, procedimientos, triggers).</summary>
    public string? DifferencesJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void Complete(int differencesCount, string differencesJson)
    {
        Status = RunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        DifferencesCount = differencesCount;
        DifferencesJson = differencesJson;
    }

    public void Fail(string error)
    {
        Status = RunStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = error;
    }
}
