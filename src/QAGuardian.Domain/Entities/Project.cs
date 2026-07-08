using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Agregado raíz: proyecto de software bajo control de calidad.</summary>
public class Project : AuditableEntity
{
    private readonly List<Module> _modules = [];
    private readonly List<ProjectVersion> _versions = [];

    private Project() { } // EF Core

    public Project(string code, string name, string? description, string? repositoryUrl)
    {
        SetCode(code);
        SetName(name);
        Description = description;
        RepositoryUrl = repositoryUrl;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? RepositoryUrl { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? QualityGateId { get; private set; }
    public QualityGate? QualityGate { get; private set; }

    public IReadOnlyCollection<Module> Modules => _modules.AsReadOnly();
    public IReadOnlyCollection<ProjectVersion> Versions => _versions.AsReadOnly();

    public void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20)
            throw new DomainException("El código del proyecto es obligatorio y no puede exceder 20 caracteres.");
        Code = code.Trim().ToUpperInvariant();
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del proyecto es obligatorio.");
        Name = name.Trim();
    }

    public void Update(string name, string? description, string? repositoryUrl)
    {
        SetName(name);
        Description = description;
        RepositoryUrl = repositoryUrl;
    }

    public void AssignQualityGate(Guid qualityGateId) => QualityGateId = qualityGateId;

    public Module AddModule(string name, string? description)
    {
        if (_modules.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !m.IsDeleted))
            throw new DomainException($"El módulo '{name}' ya existe en el proyecto.");
        var module = new Module(Id, name, description);
        _modules.Add(module);
        return module;
    }

    public ProjectVersion AddVersion(string number, string? notes)
    {
        if (_versions.Any(v => v.Number == number))
            throw new DomainException($"La versión '{number}' ya existe en el proyecto.");
        var version = new ProjectVersion(Id, number, notes);
        _versions.Add(version);
        return version;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
