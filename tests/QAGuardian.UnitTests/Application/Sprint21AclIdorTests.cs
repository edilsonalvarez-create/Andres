using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.AuditLog;
using QAGuardian.Application.Features.QualityGates;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Identity;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Application;

/// <summary>
/// B6 / ADR-013: IDOR audit log + ProjectAdmin en assign gate (userA vs userB).
/// </summary>
public class Sprint21AclIdorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QAGuardianDbContext _context;
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ProjectAccessService _access;

    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectAdminA = Guid.NewGuid();
    private readonly Guid _projectA;
    private readonly Guid _projectB;
    private readonly Guid _gateId;

    public Sprint21AclIdorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new QAGuardianDbContext(options);
        _context.Database.EnsureCreated();

        var projectA = new Project("PA", "Proyecto A", null, null);
        var projectB = new Project("PB", "Proyecto B", null, null);
        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _context.Projects.AddRange(projectA, projectB);
        _context.ProjectMembers.Add(new ProjectMember(_projectA, _userA, RoleInProject.Member));
        _context.ProjectMembers.Add(new ProjectMember(_projectA, _projectAdminA, RoleInProject.ProjectAdmin));
        _context.ProjectMembers.Add(new ProjectMember(_projectB, _userB, RoleInProject.Member));

        var gate = new QualityGate("Gate B6", false);
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 80, true);
        _gateId = gate.Id;
        _context.QualityGates.Add(gate);

        _context.AuditLogs.AddRange(
            new AuditLog(null, "a@x", "POST", $"/api/v1/Projects/{_projectA}/modules", null, null, null, null),
            new AuditLog(null, "b@x", "POST", $"/api/v1/Projects/{_projectB}/modules", null, null, null, null),
            new AuditLog(null, "sys@x", "POST", "/api/v1/QualityGates", null, null, null, null));

        _context.SaveChanges();

        _access = new ProjectAccessService(
            _currentUser,
            new ProjectMemberRepository(_context),
            new ProjectRepository(_context),
            new TestRunRepository(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void AsUser(Guid userId, bool admin = false)
    {
        _currentUser.UserId.Returns(userId);
        _currentUser.IsInRole(SystemRoles.Administrator).Returns(admin);
    }

    [Fact]
    public async Task AuditLog_userA_no_ve_entradas_del_proyecto_B_ni_globales()
    {
        AsUser(_userA);
        var handler = new GetAuditLogQueryHandler(new Repository<AuditLog>(_context), _access);

        var page = await handler.Handle(new GetAuditLogQuery(Page: 1, PageSize: 50), default);

        page.Items.Should().ContainSingle(i => i.EntityName.Contains(_projectA.ToString()));
        page.Items.Should().NotContain(i => i.EntityName.Contains(_projectB.ToString()));
        page.Items.Should().NotContain(i => i.EntityName.Equals("/api/v1/QualityGates"));
    }

    [Fact]
    public async Task AuditLog_admin_ve_todas_las_entradas()
    {
        AsUser(_userA, admin: true);
        var handler = new GetAuditLogQueryHandler(new Repository<AuditLog>(_context), _access);

        var page = await handler.Handle(new GetAuditLogQuery(Page: 1, PageSize: 50), default);

        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GateAuditLog_no_admin_recibe_404()
    {
        AsUser(_userA);
        var handler = new GetQualityGateAuditLogQueryHandler(new Repository<AuditLog>(_context), _access);

        await handler.Invoking(h => h.Handle(new GetQualityGateAuditLogQuery(_gateId), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AssignGate_Member_bloqueado_ProjectAdmin_ok()
    {
        var projects = new ProjectRepository(_context);
        var gates = new QualityGateRepository(_context);
        var uow = new UnitOfWork(_context);
        var handler = new AssignGateToProjectCommandHandler(projects, gates, _access, uow);

        AsUser(_userA);
        await handler.Invoking(h => h.Handle(new AssignGateToProjectCommand(_projectA, _gateId), default))
            .Should().ThrowAsync<NotFoundException>();

        AsUser(_projectAdminA);
        var result = await handler.Handle(new AssignGateToProjectCommand(_projectA, _gateId), default);
        result.IsSuccess.Should().BeTrue();
    }
}
