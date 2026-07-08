using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Agregado raíz: defecto detectado manual o automáticamente.</summary>
public class Defect : AuditableEntity
{
    private Defect() { } // EF Core

    public Defect(
        Guid projectId,
        string code,
        string title,
        string description,
        DefectSeverity severity,
        DefectPriority priority,
        Guid reportedByUserId,
        Guid? moduleId = null,
        Guid? testResultId = null,
        string? sprint = null,
        string? version = null,
        string? stackTrace = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("El código del defecto es obligatorio.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("El título del defecto es obligatorio.");
        ProjectId = projectId;
        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        Description = description;
        Severity = severity;
        Priority = priority;
        ReportedByUserId = reportedByUserId;
        ModuleId = moduleId;
        TestResultId = testResultId;
        Sprint = sprint;
        Version = version;
        StackTrace = stackTrace;
        Status = DefectStatus.New;
    }

    public Guid ProjectId { get; private set; }
    public Guid? ModuleId { get; private set; }
    public Guid? TestResultId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public DefectSeverity Severity { get; private set; }
    public DefectPriority Priority { get; private set; }
    public DefectStatus Status { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? Sprint { get; private set; }
    public string? Version { get; private set; }
    public string? StackTrace { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public void Assign(Guid userId)
    {
        if (Status is DefectStatus.Closed)
            throw new DomainException("No se puede asignar un defecto cerrado.");
        AssignedToUserId = userId;
        if (Status == DefectStatus.New) Status = DefectStatus.Assigned;
    }

    public void StartProgress()
    {
        if (Status is not (DefectStatus.Assigned or DefectStatus.Reopened))
            throw new DomainException("Solo se puede iniciar un defecto asignado o reabierto.");
        Status = DefectStatus.InProgress;
    }

    public void Resolve()
    {
        if (Status is not (DefectStatus.InProgress or DefectStatus.Assigned or DefectStatus.Reopened))
            throw new DomainException("Solo se puede resolver un defecto en progreso, asignado o reabierto.");
        Status = DefectStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Verify()
    {
        if (Status != DefectStatus.Resolved)
            throw new DomainException("Solo se puede verificar un defecto resuelto.");
        Status = DefectStatus.Verified;
    }

    public void Close()
    {
        if (Status is not (DefectStatus.Verified or DefectStatus.Resolved or DefectStatus.Rejected))
            throw new DomainException("Solo se puede cerrar un defecto verificado, resuelto o rechazado.");
        Status = DefectStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status is not (DefectStatus.Resolved or DefectStatus.Verified or DefectStatus.Closed))
            throw new DomainException("Solo se puede reabrir un defecto resuelto, verificado o cerrado.");
        Status = DefectStatus.Reopened;
        ResolvedAt = null;
        ClosedAt = null;
    }

    public void Reject() => Status = DefectStatus.Rejected;

    public void UpdateClassification(DefectSeverity severity, DefectPriority priority)
    {
        Severity = severity;
        Priority = priority;
    }
}
