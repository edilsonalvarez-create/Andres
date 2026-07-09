using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Dashboard;

public record ModuleErrorStat(string ModuleName, int FailedCount);
public record TrendPoint(DateTime Date, int Passed, int Failed, decimal PassRate);
/// <summary>Celda del heatmap de fallos: módulo × día.</summary>
public record HeatmapCell(string ModuleName, string Date, int FailedCount);

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
    decimal AvailabilityPercent,
    IReadOnlyList<ModuleErrorStat> ErrorsByModule,
    IReadOnlyList<HeatmapCell> Heatmap,
    IReadOnlyList<TrendPoint> Trend);

/// <summary>KPIs del dashboard, opcionalmente acotados por proyecto y rango de fechas.</summary>
public record GetDashboardStatsQuery(
    Guid? ProjectId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null) : IRequest<DashboardDto>;

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
        var pid = request.ProjectId;
        var since = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var until = request.ToDate ?? DateTime.UtcNow;

        var cacheKey = $"{CacheKeyPrefix}{pid?.ToString() ?? "global"}:{since:yyyyMMdd}:{until:yyyyMMdd}";
        var cached = await _cache.GetAsync<DashboardDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var totalProjects = await _projects.CountAsync(p => !p.IsDeleted && p.IsActive, ct);
        var totalTestCases = await _testCases.CountAsync(
            tc => !tc.IsDeleted && (pid == null || tc.ProjectId == pid), ct);
        var automated = await _testCases.CountAsync(
            tc => !tc.IsDeleted && tc.Framework != AutomationFramework.Manual && (pid == null || tc.ProjectId == pid), ct);

        var recentRuns = await _runs.ListAsync(
            r => !r.IsDeleted && r.CreatedAt >= since && r.CreatedAt <= until
                && (pid == null || r.ProjectId == pid), ct);

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

        // Disponibilidad del pipeline: % de ejecuciones que finalizaron correctamente
        // (Completed) frente al total lanzado (excluye fallidas y canceladas).
        var terminalRunCount = recentRuns.Count(r => r.Status is RunStatus.Completed
            or RunStatus.Failed or RunStatus.Cancelled);
        var completedRunCount = recentRuns.Count(r => r.Status == RunStatus.Completed);
        var availability = terminalRunCount == 0 ? 100m
            : Math.Round(completedRunCount * 100m / terminalRunCount, 2);

        var moduleFailures = await ComputeModuleFailuresAsync(withResults, ct);
        var errorsByModule = moduleFailures
            .GroupBy(f => f.Module)
            .Select(g => new ModuleErrorStat(g.Key, g.Sum(x => x.Count)))
            .OrderByDescending(s => s.FailedCount)
            .ToList();
        var heatmap = moduleFailures
            .Select(f => new HeatmapCell(f.Module, f.Date.ToString("yyyy-MM-dd"), f.Count))
            .OrderBy(c => c.Date).ThenBy(c => c.ModuleName)
            .ToList();
        var trend = ComputeTrend(withResults);

        var dto = new DashboardDto(totalProjects, totalTestCases, automated,
            totalTestCases == 0 ? 0 : Math.Round(automated * 100m / totalTestCases, 2),
            recentRuns.Count, executed, passed, failed, pending, passRate,
            avgDuration, openDefects, criticalOpen, vulns, qualityScore, availability,
            errorsByModule, heatmap, trend);

        await _cache.SetAsync(cacheKey, dto, CacheTtl, ct);
        return dto;
    }

    /// <summary>Fallos agrupados por (módulo, día); base de "errores por módulo" y del heatmap.</summary>
    private async Task<List<(string Module, DateTime Date, int Count)>> ComputeModuleFailuresAsync(
        List<TestRun> runs, CancellationToken ct)
    {
        var failed = runs.SelectMany(r => r.Results)
            .Where(res => res.Status == ResultStatus.Failed)
            .ToList();
        if (failed.Count == 0) return [];

        var caseIds = failed.Where(res => res.TestCaseId.HasValue)
            .Select(res => res.TestCaseId!.Value).Distinct().ToList();
        var cases = await _testCases.ListAsync(tc => caseIds.Contains(tc.Id), ct);
        var caseModule = cases.ToDictionary(tc => tc.Id, tc => tc.ModuleId);
        var moduleIds = cases.Where(tc => tc.ModuleId.HasValue).Select(tc => tc.ModuleId!.Value).Distinct().ToList();
        var modules = await _modules.ListAsync(m => moduleIds.Contains(m.Id), ct);
        var moduleNames = modules.ToDictionary(m => m.Id, m => m.Name);

        string ModuleOf(TestResult res)
        {
            if (res.TestCaseId is { } id && caseModule.TryGetValue(id, out var modId)
                && modId is { } m && moduleNames.TryGetValue(m, out var name))
                return name;
            return "Sin módulo";
        }

        return failed
            .GroupBy(res => new { Module = ModuleOf(res), Date = res.ExecutedAt.Date })
            .Select(g => (g.Key.Module, g.Key.Date, g.Count()))
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
