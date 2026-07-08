using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Módulo funcional de un proyecto.</summary>
public class Module : AuditableEntity
{
    private readonly List<Requirement> _requirements = [];

    private Module() { } // EF Core

    public Module(Guid projectId, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del módulo es obligatorio.");
        ProjectId = projectId;
        Name = name.Trim();
        Description = description;
    }

    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    public IReadOnlyCollection<Requirement> Requirements => _requirements.AsReadOnly();

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del módulo es obligatorio.");
        Name = name.Trim();
        Description = description;
    }

    public Requirement AddRequirement(string code, string title, string? description)
    {
        if (_requirements.Any(r => r.Code == code && !r.IsDeleted))
            throw new DomainException($"El requerimiento '{code}' ya existe en el módulo.");
        var requirement = new Requirement(Id, code, title, description);
        _requirements.Add(requirement);
        return requirement;
    }
}
