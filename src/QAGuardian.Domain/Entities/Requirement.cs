using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Requerimiento funcional o no funcional asociado a un módulo.</summary>
public class Requirement : AuditableEntity
{
    private readonly List<UserStory> _userStories = [];

    private Requirement() { } // EF Core

    public Requirement(Guid moduleId, string code, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("El código del requerimiento es obligatorio.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("El título del requerimiento es obligatorio.");
        ModuleId = moduleId;
        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        Description = description;
    }

    public Guid ModuleId { get; private set; }
    public Module Module { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }

    public IReadOnlyCollection<UserStory> UserStories => _userStories.AsReadOnly();

    public UserStory AddUserStory(string title, string? acceptanceCriteria)
    {
        var story = new UserStory(Id, title, acceptanceCriteria);
        _userStories.Add(story);
        return story;
    }
}
