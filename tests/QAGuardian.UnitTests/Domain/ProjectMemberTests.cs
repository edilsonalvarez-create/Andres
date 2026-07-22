using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class ProjectMemberTests
{
    [Fact]
    public void Constructor_crea_membresia_activa_con_rol_Member_por_defecto()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = new ProjectMember(projectId, userId);

        member.ProjectId.Should().Be(projectId);
        member.UserId.Should().Be(userId);
        member.RoleInProject.Should().Be(RoleInProject.Member);
        member.IsActive.Should().BeTrue();
        member.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_acepta_ProjectAdmin()
    {
        var member = new ProjectMember(Guid.NewGuid(), Guid.NewGuid(), RoleInProject.ProjectAdmin);
        member.RoleInProject.Should().Be(RoleInProject.ProjectAdmin);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Constructor_rechaza_ids_vacios(bool emptyProject, bool emptyUser)
    {
        var projectId = emptyProject ? Guid.Empty : Guid.NewGuid();
        var userId = emptyUser ? Guid.Empty : Guid.NewGuid();

        var act = () => new ProjectMember(projectId, userId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ChangeRole_y_Deactivate_Activate_actualizan_estado()
    {
        var member = new ProjectMember(Guid.NewGuid(), Guid.NewGuid());

        member.ChangeRole(RoleInProject.ProjectAdmin);
        member.RoleInProject.Should().Be(RoleInProject.ProjectAdmin);

        member.Deactivate();
        member.IsActive.Should().BeFalse();

        member.Activate();
        member.IsActive.Should().BeTrue();
    }
}
