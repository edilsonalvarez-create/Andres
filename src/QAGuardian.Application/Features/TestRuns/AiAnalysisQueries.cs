using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.TestRuns;

/// <summary>Diagnóstico IA persistido para un fallo de una ejecución (solo lectura; no re-invoca LLM).</summary>
public record AiAnalysisDto(
    Guid Id,
    Guid? TestResultId,
    string? TestName,
    string Summary,
    string ProbableCause,
    double? Confidence,
    string? EvidenceQuote,
    IReadOnlyList<string> Recommendations,
    RiskLevel Criticality,
    DefectPriority SuggestedPriority,
    string SuggestedOwnerRole,
    string ModelUsed,
    DateTime CreatedAt);

public record GetAiAnalysisForTestRunQuery(Guid TestRunId) : IRequest<IReadOnlyList<AiAnalysisDto>>;

public class GetAiAnalysisForTestRunQueryHandler
    : IRequestHandler<GetAiAnalysisForTestRunQuery, IReadOnlyList<AiAnalysisDto>>
{
    private readonly ITestRunRepository _runs;
    private readonly IRepository<AiAnalysis> _analyses;
    private readonly IProjectAccessService _access;

    public GetAiAnalysisForTestRunQueryHandler(
        ITestRunRepository runs,
        IRepository<AiAnalysis> analyses,
        IProjectAccessService access)
    {
        _runs = runs;
        _analyses = analyses;
        _access = access;
    }

    public async Task<IReadOnlyList<AiAnalysisDto>> Handle(
        GetAiAnalysisForTestRunQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessTestRunAsync(request.TestRunId, ct);

        var run = await _runs.GetWithResultsAsync(request.TestRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.TestRunId);

        var resultIds = run.Results.Select(r => r.Id).ToHashSet();
        if (resultIds.Count == 0)
            return Array.Empty<AiAnalysisDto>();

        var nameByResultId = run.Results.ToDictionary(r => r.Id, r => r.Name);
        var analyses = await _analyses.ListAsync(
            a => a.TestResultId != null && resultIds.Contains(a.TestResultId.Value), ct);

        return analyses
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a, a.TestResultId is Guid rid && nameByResultId.TryGetValue(rid, out var n) ? n : null))
            .ToList();
    }

    private static AiAnalysisDto ToDto(AiAnalysis a, string? testName)
    {
        var recommendations = string.IsNullOrWhiteSpace(a.Recommendation)
            ? Array.Empty<string>()
            : a.Recommendation
                .Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        if (recommendations.Length == 0 && !string.IsNullOrWhiteSpace(a.Recommendation))
            recommendations = [a.Recommendation.Trim()];

        return new AiAnalysisDto(
            a.Id,
            a.TestResultId,
            testName,
            a.Diagnosis,
            a.ProbableCause,
            a.Confidence,
            a.EvidenceQuote,
            recommendations,
            a.Criticality,
            a.SuggestedPriority,
            a.SuggestedOwnerRole,
            a.ModelUsed,
            a.CreatedAt);
    }
}
