using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Traceability;

public record CoverageCaseDto(
    Guid Id, string Code, string Title, TestCaseStatus Status,
    ResultStatus? LastResultStatus, DateTime? LastExecutedAt,
    IReadOnlyList<string> OpenDefectCodes);

public record CoverageStoryDto(
    Guid Id, string Title, int TestCaseCount, int PassedCount, int FailedCount,
    string CoverageStatus, IReadOnlyList<CoverageCaseDto> TestCases);

public record CoverageRequirementDto(
    Guid Id, string Code, string Title, Guid ModuleId, string ModuleName,
    int StoryCount, int CoveredStoryCount, IReadOnlyList<CoverageStoryDto> Stories);

public record RequirementsCoverageDto(
    Guid ProjectId,
    int TotalRequirements,
    int TotalStories,
    int CoveredStories,
    int UncoveredStories,
    decimal CoveragePercent,
    IReadOnlyList<CoverageRequirementDto> Requirements);

public record GetRequirementsCoverageQuery(Guid ProjectId) : IRequest<RequirementsCoverageDto>;

/// <summary>
/// Matriz de trazabilidad Req → Historia → Caso → Último resultado → Defectos abiertos
/// sobre el catálogo interno (módulos / requerimientos / historias / casos).
/// </summary>
public class GetRequirementsCoverageQueryHandler
    : IRequestHandler<GetRequirementsCoverageQuery, RequirementsCoverageDto>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IRepository<Module> _modules;
    private readonly IRepository<Requirement> _requirements;
    private readonly IRepository<UserStory> _stories;
    private readonly ITestCaseRepository _testCases;
    private readonly IRepository<TestResult> _results;
    private readonly IDefectRepository _defects;

    public GetRequirementsCoverageQueryHandler(
        IProjectRepository projects,
        IProjectAccessService access,
        IRepository<Module> modules,
        IRepository<Requirement> requirements,
        IRepository<UserStory> stories,
        ITestCaseRepository testCases,
        IRepository<TestResult> results,
        IDefectRepository defects)
    {
        _projects = projects;
        _access = access;
        _modules = modules;
        _requirements = requirements;
        _stories = stories;
        _testCases = testCases;
        _results = results;
        _defects = defects;
    }

    public async Task<RequirementsCoverageDto> Handle(
        GetRequirementsCoverageQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var modules = await _modules.ListAsync(m => m.ProjectId == request.ProjectId && !m.IsDeleted, ct);
        var moduleIds = modules.Select(m => m.Id).ToHashSet();
        var moduleNames = modules.ToDictionary(m => m.Id, m => m.Name);

        var requirements = (await _requirements.ListAsync(
                r => moduleIds.Contains(r.ModuleId) && !r.IsDeleted, ct))
            .OrderBy(r => r.Code)
            .ToList();
        var reqIds = requirements.Select(r => r.Id).ToHashSet();

        var stories = (await _stories.ListAsync(
                s => reqIds.Contains(s.RequirementId) && !s.IsDeleted, ct))
            .OrderBy(s => s.Title)
            .ToList();
        var storyIds = stories.Select(s => s.Id).ToHashSet();

        var cases = (await _testCases.ListAsync(
                tc => tc.ProjectId == request.ProjectId
                      && tc.UserStoryId != null
                      && storyIds.Contains(tc.UserStoryId.Value)
                      && !tc.IsDeleted, ct))
            .ToList();
        var caseIds = cases.Select(c => c.Id).ToHashSet();

        var results = caseIds.Count == 0
            ? []
            : await _results.ListAsync(r => r.TestCaseId != null && caseIds.Contains(r.TestCaseId.Value), ct);

        var lastByCase = results
            .Where(r => r.TestCaseId.HasValue)
            .GroupBy(r => r.TestCaseId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.ExecutedAt).First());

        var resultIds = lastByCase.Values.Select(r => r.Id).ToHashSet();
        var defects = resultIds.Count == 0
            ? []
            : await _defects.ListAsync(
                d => d.TestResultId != null
                     && resultIds.Contains(d.TestResultId.Value)
                     && d.Status != DefectStatus.Closed
                     && d.Status != DefectStatus.Rejected
                     && !d.IsDeleted, ct);

        var defectsByResult = defects
            .Where(d => d.TestResultId.HasValue)
            .GroupBy(d => d.TestResultId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Code).ToList());

        var casesByStory = cases
            .Where(c => c.UserStoryId.HasValue)
            .GroupBy(c => c.UserStoryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var storiesByReq = stories.GroupBy(s => s.RequirementId).ToDictionary(g => g.Key, g => g.ToList());

        var reqDtos = new List<CoverageRequirementDto>();
        var totalStories = 0;
        var coveredStories = 0;

        foreach (var req in requirements)
        {
            var reqStories = storiesByReq.GetValueOrDefault(req.Id) ?? [];
            var storyDtos = new List<CoverageStoryDto>();

            foreach (var story in reqStories)
            {
                totalStories++;
                var storyCases = casesByStory.GetValueOrDefault(story.Id) ?? [];
                if (storyCases.Count > 0) coveredStories++;

                var caseDtos = storyCases.Select(tc =>
                {
                    lastByCase.TryGetValue(tc.Id, out var last);
                    var openCodes = last is not null && defectsByResult.TryGetValue(last.Id, out var codes)
                        ? (IReadOnlyList<string>)codes
                        : Array.Empty<string>();
                    return new CoverageCaseDto(
                        tc.Id, tc.Code, tc.Title, tc.Status,
                        last?.Status, last?.ExecutedAt, openCodes);
                }).ToList();

                var passed = caseDtos.Count(c => c.LastResultStatus == ResultStatus.Passed);
                var failed = caseDtos.Count(c => c.LastResultStatus == ResultStatus.Failed);
                var coverageStatus = storyCases.Count == 0 ? "Uncovered"
                    : failed > 0 ? "Failed"
                    : passed == storyCases.Count && storyCases.Count > 0 ? "Passed"
                    : caseDtos.Any(c => c.LastResultStatus is null) ? "NotExecuted"
                    : "Covered";

                storyDtos.Add(new CoverageStoryDto(
                    story.Id, story.Title, storyCases.Count, passed, failed,
                    coverageStatus, caseDtos));
            }

            reqDtos.Add(new CoverageRequirementDto(
                req.Id, req.Code, req.Title, req.ModuleId,
                moduleNames.GetValueOrDefault(req.ModuleId, "?"),
                storyDtos.Count,
                storyDtos.Count(s => s.TestCaseCount > 0),
                storyDtos));
        }

        var coveragePercent = totalStories == 0
            ? 0m
            : Math.Round(coveredStories * 100m / totalStories, 1);

        return new RequirementsCoverageDto(
            request.ProjectId,
            requirements.Count,
            totalStories,
            coveredStories,
            totalStories - coveredStories,
            coveragePercent,
            reqDtos);
    }
}
