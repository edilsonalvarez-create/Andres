using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Historia de usuario derivada de un requerimiento.</summary>
public class UserStory : AuditableEntity
{
    private UserStory() { } // EF Core

    public UserStory(Guid requirementId, string title, string? acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("El título de la historia de usuario es obligatorio.");
        RequirementId = requirementId;
        Title = title.Trim();
        AcceptanceCriteria = acceptanceCriteria;
    }

    public Guid RequirementId { get; private set; }
    public Requirement Requirement { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? AcceptanceCriteria { get; private set; }
}
