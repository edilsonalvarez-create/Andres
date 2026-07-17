using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>
/// Membresía de un usuario en un proyecto (ACL multi-tenant — Threat Model TM-01).
/// La capacidad de acción sigue en <see cref="SystemRoles"/>; esta entidad controla pertenencia/visibilidad.
/// </summary>
public class ProjectMember : AuditableEntity
{
    private ProjectMember() { } // EF Core

    public ProjectMember(Guid projectId, Guid userId, RoleInProject roleInProject = RoleInProject.Member)
    {
        if (projectId == Guid.Empty)
            throw new DomainException("El proyecto de la membresía es obligatorio.");
        if (userId == Guid.Empty)
            throw new DomainException("El usuario de la membresía es obligatorio.");

        ProjectId = projectId;
        UserId = userId;
        RoleInProject = roleInProject;
        IsActive = true;
    }

    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public RoleInProject RoleInProject { get; private set; }
    public bool IsActive { get; private set; }

    public void ChangeRole(RoleInProject roleInProject) => RoleInProject = roleInProject;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
