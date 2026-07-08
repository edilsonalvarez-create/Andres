using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Rol RBAC. Los roles del sistema se definen en <see cref="SystemRoles"/>.</summary>
public class Role : BaseEntity
{
    private Role() { } // EF Core

    public Role(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del rol es obligatorio.");
        Name = name.Trim();
        Description = description;
    }

    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
}

/// <summary>Roles predefinidos del sistema.</summary>
public static class SystemRoles
{
    public const string Administrator = "Administrador";
    public const string QA = "QA";
    public const string Developer = "Desarrollador";
    public const string TechLead = "LiderTecnico";
    public const string DevOps = "DevOps";
    public const string ProductOwner = "ProductOwner";
    public const string Auditor = "Auditor";
    public const string Client = "Cliente";

    public static readonly string[] All =
        [Administrator, QA, Developer, TechLead, DevOps, ProductOwner, Auditor, Client];
}
