using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Sprint 15-A: listado de runs no hidrata filas TestResult.</summary>
public class TestRunListSummaryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QAGuardianDbContext _context;
    private readonly TestRunRepository _runs;

    public TestRunListSummaryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new QAGuardianDbContext(options);
        _context.Database.EnsureCreated();
        _runs = new TestRunRepository(_context);
    }

    [Fact]
    public async Task PagedSummaryAsync_returns_counts_without_hydrating_Results()
    {
        var project = new Project("P15", "Sprint15", null, null);
        _context.Projects.Add(project);
        var run = new TestRun(project.Id, TestType.Smoke, EnvironmentType.QA, "tester");
        run.Start();
        run.AddResult("ok", ResultStatus.Passed, 10);
        run.AddResult("fail", ResultStatus.Failed, 20);
        run.AddResult("skip", ResultStatus.Skipped, 5);
        run.Complete();
        _context.TestRuns.Add(run);
        await _context.SaveChangesAsync();

        var (items, total) = await _runs.PagedSummaryAsync(1, 20, project.Id);

        total.Should().Be(1);
        var row = items.Should().ContainSingle().Subject;
        row.TotalTests.Should().Be(3);
        row.Passed.Should().Be(1);
        row.Failed.Should().Be(1);
        row.Skipped.Should().Be(1);

        // Carga AsNoTracking sin Include: Results debe venir vacío (no hidratado).
        var listed = await _context.TestRuns.AsNoTracking().FirstAsync(r => r.Id == run.Id);
        listed.Results.Should().BeEmpty("PagedSummary no debe Include(Results)");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
