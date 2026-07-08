using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Dashboard;

public record ModuleErrorStat(string ModuleName, int FailedCount);
public record TrendPoint(DateTime Date, int Passed, int Failed, decimal PassRate);

public record DashboardDto(
    int TotalProjects,
    int TotalTestCases,
    int AutomatedTestCases,
    decimal AutomationCoveragePercent,
    int RunsLast30Days,
    int TestsExecuted,
    int TestsPassed,
    int TestsFailed,
    int TestsPending,
    decimal PassRatePercent,
    double AvgRunDurationSeconds,
    int OpenDefects,
    int CriticalDefectsOpen,
    int VulnerabilitiesHighOrCritical,
    decimal QualityScore,
    IReadOnlyList<ModuleErrorStat> ErrorsByModule,
    IReadOnlyList<TrendPoint> Trend);

public record GetDashboardStatsQuery(Guid? ProjectId = null) : IRequest<DashboardDto>;

public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardDto>
{
    private const string CacheKeyPrefix = "dashboard:stats:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly IProjectRepository _projects;
    private readonly ITestCaseRepository _testCases;
    private readonly ITestRunRepository _runs;
    private readonly IDefectRepository _defects;
    private readonly IRepository<SecurityFinding> _findings;
    private readonly IRepository<Module> _modules;
    private readonly ICacheService _cache;

    public GetDashboardStatsQueryHandler(
        IProjectRepository projects, ITestCaseRepository testCases, ITestRunRepository runs,
        IDefectRepository defects, IRepository<SecurityFinding> findings,
        IRepository<Module> modules, ICacheService cache)
    {
        _projects = projects;
        _testCases = testCases;
        _runs = runs;
        _defects = defects;
        _findings = findings;
        _modules = modules;
        _cache = cache;
    }

    public async Task<DashboardDto> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var cacheKey = CacheKeyPrefix + (request.ProjectId?.ToString() ?? "global");
        var cached = await _cache.GetAsync<DashboardDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var pid = request.ProjectId;
        var since = DateTime.UtcNow.AddDays(-30);

        var totalProjects = await _projects.CountAsync(p => !p.IsDeleted && p.IsActive, ct);
        var totalTestCases = await _testCases.CountAsync(
            tc => !tc.IsDeleted && (pid == null || tc.ProjectId == pid), ct);
        var automated = await _testCases.CountAsync(
            tc => !tc.IsDeleted && tc.Framework != AutomationFramework.Manual && (pid == null || tc.ProjectId == pid), ct);

        var recentRuns = await _runs.ListAsync(
            r => !r.IsDeleted && r.CreatedAt >= since && (pid == null || r.ProjectId == pid), ct);

        var completedRuns = recentRuns.Where(r => r.Status == RunStatus.Completed).ToList();
        var withResults = new List<TestRun>();
        foreach (var run in completedRuns)
        {
            var full = await _runs.GetWithResultsAsync(run.Id, ct);
            if (full is not null) withResults.Add(full);
        }

        var executed = withResults.Sum(r => r.TotalTests);
        var passed = withResults.Sum(r => r.Passed);
        var failed = withResults.Sum(r => r.Failed);
        var pending = recentRuns.Count(r => r.Status == RunStatus.Pending);
        var passRate = executed == 0 ? 0 : Math.Round(passed * 100m / executed, 2);
        var avgDuration = withResults.Count == 0 ? 0
            : withResults.Where(r => r.DurationSeconds.HasValue).Select(r => r.DurationSeconds!.Value)
                .DefaultIfEmpty(0).Average();

        var openStatuses = new[] { DefectStatus.New, DefectStatus.Assigned, DefectStatus.InProgress, DefectStatus.Reopened };
        var openDefects = await _defects.CountAsync(
            d => !d.IsDeleted && openStatuses.Contains(d.Status) && (pid == null || d.ProjectId == pid), ct);
        var criticalOpen = await _defects.CountAsync(
            d => !d.IsDeleted && openStatuses.Contains(d.Status)
                && d.Severity >= DefectSeverity.Critical && (pid == null || d.ProjectId == pid), ct);

        var runIds = recentRuns.Select(r => r.Id).ToList();
        var vulns = await _findings.CountAsync(
            f => runIds.Contains(f.TestRunId) && f.Risk >= RiskLevel.High, ct);

        // Índice de calidad: pondera éxito de pruebas, defectos críticos y vulnerabilidades.
        var qualityScore = Math.Max(0, Math.Round(
            passRate - criticalOpen * 5 - vulns * 3, 2));

        var errorsByModule = await ComputeErrorsByModuleAsync(withResults, pid, ct);
        var trend = ComputeTrend(withResults);

        var dto = new DashboardDto(totalProjects, totalTestCases, automated,
            totalTestCases == 0 ? 0 : Math.Round(automated * 100m / totalTestCases, 2),
            recentRuns.Count, executed, passed, failed, pending, passRate,
            avgDuration, openDefects, criticalOpen, vulns, qualityScore, errorsByModule, trend);

        await _cache.SetAsync(cacheKey, dto, CacheTtl, ct);
        return dto;
    }

    private async Task<IReadOnlyList<ModuleErrorStat>> ComputeErrorsByModuleAsync(
        List<TestRun> runs, Guid? projectId, CancellationToken ct)
    {
        var failedCaseIds = runs.SelectMany(r => r.Results)
            .Where(res => res.Status == ResultStatus.Failed && res.TestCaseId.HasValue)
            .Select(res => res.TestCaseId!.Value)
            .ToList();
        if (failedCaseIds.Count == 0) return [];

        var failedCases = await _testCases.ListAsync(tc => failedCaseIds.Contains(tc.Id), ct);
        var moduleIds = failedCases.Where(tc => tc.ModuleId.HasValue).Select(tc => tc.ModuleId!.Value).Distinct().ToList();
        var modules = await _modules.ListAsync(m => moduleIds.Contains(m.Id), ct);
        var moduleNames = modules.ToDictionary(m => m.Id, m => m.Name);

        return failedCases
            .GroupBy(tc => tc.ModuleId.HasValue && moduleNames.TryGetValue(tc.ModuleId.Value, out var name)
                ? name : "Sin módulo")
            .Select(g => new ModuleErrorStat(g.Key, g.Count()))
            .OrderByDescending(s => s.FailedCount)
            .ToList();
    }

    private static IReadOnlyList<TrendPoint> ComputeTrend(List<TestRun> runs) =>
        runs.Where(r => r.CompletedAt.HasValue)
            .GroupBy(r => r.CompletedAt!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var passed = g.Sum(r => r.Passed);
                var total = g.Sum(r => r.TotalTests);
                return new TrendPoint(g.Key, passed, g.Sum(r => r.Failed),
                    total == 0 ? 0 : Math.Round(passed * 100m / total, 2));
            })
            .ToList();
}
