using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.TestCases;
using QAGuardian.Application.Features.TestRuns;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Identity;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Application;

/// <summary>
/// Suite IDOR Sprint 11 VERIFY: userA (proyecto A) vs userB (proyecto B)
/// — projects, runs, cases, evidence download.
/// </summary>
public class Sprint11IdorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QAGuardianDbContext _context;
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IEvidenceStorage _storage = Substitute.For<IEvidenceStorage>();
    private readonly ProjectAccessService _access;

    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _projectA;
    private readonly Guid _projectB;
    private readonly Guid _runA;
    private readonly Guid _runB;
    private readonly Guid _caseA;
    private readonly Guid _caseB;
    private readonly Guid _evidenceA;
    private readonly Guid _evidenceB;
    private const string PathA = "runs/a/shot.png";
    private const string PathB = "runs/b/shot.png";

    public Sprint11IdorTests()
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

        var caseA = new TestCase(_projectA, "TC-A", "Caso A", TestType.Functional, TestPriority.High);
        var caseB = new TestCase(_projectB, "TC-B", "Caso B", TestType.Functional, TestPriority.High);
        _caseA = caseA.Id;
        _caseB = caseB.Id;
        _context.TestCases.AddRange(caseA, caseB);

        var runA = new TestRun(_projectA, TestType.Smoke, EnvironmentType.QA, "userA");
        var runB = new TestRun(_projectB, TestType.Smoke, EnvironmentType.QA, "userB");
        _runA = runA.Id;
        _runB = runB.Id;
        runA.Start();
        runB.Start();
        var resultA = runA.AddResult("t", ResultStatus.Failed, 10);
        var resultB = runB.AddResult("t", ResultStatus.Failed, 10);
        var evA = resultA.AttachEvidence(EvidenceType.Screenshot, PathA, "image/png", 100);
        var evB = resultB.AttachEvidence(EvidenceType.Screenshot, PathB, "image/png", 100);
        _evidenceA = evA.Id;
        _evidenceB = evB.Id;
        runA.Complete();
        runB.Complete();
        _context.TestRuns.AddRange(runA, runB);
        _context.SaveChanges();

        _access = new ProjectAccessService(
            _currentUser,
            new ProjectMemberRepository(_context),
            new ProjectRepository(_context),
            new TestRunRepository(_context));

        _storage.OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new MemoryStream("fake"u8.ToArray()));
        _currentUser.IsInRole(SystemRoles.QA).Returns(true);
        _currentUser.IsInRole(SystemRoles.Administrator).Returns(false);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void As(Guid userId) => _currentUser.UserId.Returns(userId);

    [Fact]
    public async Task Projects_userA_no_lee_proyecto_B()
    {
        As(_userA);
        await _access.Invoking(a => a.EnsureCanAccessProjectAsync(_projectB))
            .Should().ThrowAsync<NotFoundException>();

        var ids = await _access.ListAccessibleProjectIdsAsync();
        ids.Should().BeEquivalentTo([_projectA]);
    }

    [Fact]
    public async Task Runs_userA_no_lee_run_de_B()
    {
        As(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new GetTestRunDetailQueryHandler(runs, _access);

        await handler.Invoking(h => h.Handle(new GetTestRunDetailQuery(_runB), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Cases_userA_no_lee_caso_de_B()
    {
        As(_userA);
        var cases = new TestCaseRepository(_context);
        var handler = new GetTestCaseByIdQueryHandler(cases, _access);

        await handler.Invoking(h => h.Handle(new GetTestCaseByIdQuery(_caseB), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Evidence_userA_no_descarga_evidenceId_de_B()
    {
        As(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new DownloadEvidenceQueryHandler(runs, _storage, _access, _currentUser);

        await handler.Invoking(h => h.Handle(new DownloadEvidenceQuery(_runB, _evidenceB), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Evidence_userA_no_usa_path_de_B_sobre_run_A()
    {
        As(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new DownloadEvidenceQueryHandler(runs, _storage, _access, _currentUser);

        // Path de B no pertenece a evidencias de run A → 404 (ownership).
        await handler.Invoking(h => h.Handle(new DownloadEvidenceQuery(_runA, Path: PathB), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Evidence_userA_descarga_su_evidence_por_id()
    {
        As(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new DownloadEvidenceQueryHandler(runs, _storage, _access, _currentUser);

        var result = await handler.Handle(new DownloadEvidenceQuery(_runA, _evidenceA), default);

        result.FileName.Should().Be("shot.png");
        result.ContentType.Should().Be("image/png");
        await result.Content.DisposeAsync();
    }

    [Fact]
    public async Task Evidence_userA_descarga_por_path_solo_si_es_del_run()
    {
        As(_userA);
        var runs = new TestRunRepository(_context);
        var handler = new DownloadEvidenceQueryHandler(runs, _storage, _access, _currentUser);

        var result = await handler.Handle(new DownloadEvidenceQuery(_runA, Path: PathA), default);
        result.FileName.Should().Be("shot.png");
        await result.Content.DisposeAsync();
    }
}
