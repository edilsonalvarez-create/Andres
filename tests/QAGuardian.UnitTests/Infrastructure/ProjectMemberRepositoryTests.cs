using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class ProjectMemberRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QAGuardianDbContext _context;
    private readonly ProjectMemberRepository _repo;

    public ProjectMemberRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new QAGuardianDbContext(options);
        _context.Database.EnsureCreated();
        _repo = new ProjectMemberRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Add_y_ListByProject_persisten_membresias()
    {
        var projectId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _repo.AddAsync(new ProjectMember(projectId, userA, RoleInProject.ProjectAdmin));
        await _repo.AddAsync(new ProjectMember(projectId, userB, RoleInProject.Member));
        await _context.SaveChangesAsync();

        var members = await _repo.ListByProjectAsync(projectId);

        members.Should().HaveCount(2);
        members.Select(m => m.UserId).Should().BeEquivalentTo([userA, userB]);
    }

    [Fact]
    public async Task GetAsync_e_IsActiveMemberAsync_respetan_proyecto_y_activo()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member = new ProjectMember(projectId, userId);

        await _repo.AddAsync(member);
        await _context.SaveChangesAsync();

        var found = await _repo.GetAsync(projectId, userId);
        found.Should().NotBeNull();
        found!.RoleInProject.Should().Be(RoleInProject.Member);

        (await _repo.IsActiveMemberAsync(projectId, userId)).Should().BeTrue();

        member.Deactivate();
        _repo.Update(member);
        await _context.SaveChangesAsync();

        (await _repo.IsActiveMemberAsync(projectId, userId)).Should().BeFalse();
    }

    [Fact]
    public async Task ListByUser_solo_devuelve_membresias_del_usuario()
    {
        var userId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        await _repo.AddAsync(new ProjectMember(p1, userId));
        await _repo.AddAsync(new ProjectMember(p2, userId));
        await _repo.AddAsync(new ProjectMember(Guid.NewGuid(), Guid.NewGuid()));
        await _context.SaveChangesAsync();

        var list = await _repo.ListByUserAsync(userId);

        list.Should().HaveCount(2);
        list.Select(m => m.ProjectId).Should().BeEquivalentTo([p1, p2]);
    }
}
