using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Versión liberable de un proyecto.</summary>
public class ProjectVersion : AuditableEntity
{
    private ProjectVersion() { } // EF Core

    public ProjectVersion(Guid projectId, string number, string? notes)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new DomainException("El número de versión es obligatorio.");
        ProjectId = projectId;
        Number = number.Trim();
        Notes = notes;
    }

    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = default!;
    public string? Notes { get; private set; }
    public DateTime? ReleasedAt { get; private set; }

    public void MarkReleased() => ReleasedAt = DateTime.UtcNow;
}
