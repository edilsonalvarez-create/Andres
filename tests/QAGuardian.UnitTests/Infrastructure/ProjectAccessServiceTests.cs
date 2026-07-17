using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Identity;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>IDOR / TM-01: usuario A no accede a recursos del proyecto B.</summary>
public class ProjectAccessServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QAGuardianDbContext _context;
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ProjectAccessService _sut;

    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectA;
    private readonly Guid _projectB;
    private readonly Guid _runB;

    public ProjectAccessServiceTests()
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
        _context.ProjectMembers.Add(new ProjectMember(_projectB, _userB, RoleInProject.Member));

        var runB = new TestRun(_projectB, TestType.Smoke, EnvironmentType.QA, "userB");
        _runB = runB.Id;
        _context.TestRuns.Add(runB);
        _context.SaveChanges();

        var members = new ProjectMemberRepository(_context);
        var projects = new ProjectRepository(_context);
        var runs = new TestRunRepository(_context);
        _sut = new ProjectAccessService(_currentUser, members, projects, runs);
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
    public async Task UsuarioA_accede_a_su_proyecto_y_no_al_de_B()
    {
        AsUser(_userA);

        (await _sut.CanAccessProjectAsync(_projectA)).Should().BeTrue();
        (await _sut.CanAccessProjectAsync(_projectB)).Should().BeFalse();

        await _sut.Invoking(s => s.EnsureCanAccessProjectAsync(_projectB))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ListAccessible_solo_devuelve_membresias_activas()
    {
        AsUser(_userA);
        var ids = await _sut.ListAccessibleProjectIdsAsync();
        ids.Should().BeEquivalentTo([_projectA]);
    }

    [Fact]
    public async Task UsuarioA_no_accede_al_TestRun_de_B()
    {
        AsUser(_userA);
        await _sut.Invoking(s => s.EnsureCanAccessTestRunAsync(_runB))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UsuarioB_accede_a_su_TestRun()
    {
        AsUser(_userB);
        var projectId = await _sut.GetAccessibleTestRunProjectIdAsync(_runB);
        projectId.Should().Be(_projectB);
    }

    [Fact]
    public async Task Admin_global_tiene_bypass_sobre_todos_los_proyectos()
    {
        AsUser(_userA, admin: true);

        (await _sut.CanAccessProjectAsync(_projectA)).Should().BeTrue();
        (await _sut.CanAccessProjectAsync(_projectB)).Should().BeTrue();

        var ids = await _sut.ListAccessibleProjectIdsAsync();
        ids.Should().BeEquivalentTo([_projectA, _projectB]);
    }

    [Fact]
    public async Task GetTestRunDetail_handler_bloquea_run_ajeno()
    {
        AsUser(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new QAGuardian.Application.Features.TestRuns.GetTestRunDetailQueryHandler(runs, _sut);

        var act = () => handler.Handle(
            new QAGuardian.Application.Features.TestRuns.GetTestRunDetailQuery(_runB), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
