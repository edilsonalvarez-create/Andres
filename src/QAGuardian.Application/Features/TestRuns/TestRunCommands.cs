using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.TestRuns;

public record TestRunDto(
    Guid Id, Guid ProjectId, TestType RunType, EnvironmentType Environment, RunStatus Status,
    string TriggeredBy, string? CommitSha, int? PullRequestNumber,
    DateTime? StartedAt, DateTime? CompletedAt, double? DurationSeconds,
    int TotalTests, int Passed, int Failed, int Skipped, decimal PassRatePercent,
    string? GateStatus, bool? DeploymentApproved, string? ErrorMessage);

public record TestResultDto(
    Guid Id, string Name, ResultStatus Status, long DurationMs,
    string? ErrorMessage, string? StackTrace, DateTime ExecutedAt,
    string? MetricsJson, IReadOnlyList<EvidenceDto> Evidences);

public record EvidenceDto(Guid Id, EvidenceType Type, string FilePath, string ContentType, long SizeBytes);

public static class TestRunMapper
{
    public static TestRunDto ToDto(this TestRun run) => new(
        run.Id, run.ProjectId, run.RunType, run.Environment, run.Status,
        run.TriggeredBy, run.CommitSha, run.PullRequestNumber,
        run.StartedAt, run.CompletedAt, run.DurationSeconds,
        run.TotalTests, run.Passed, run.Failed, run.Skipped, run.PassRatePercent,
        run.GateEvaluation?.Status.ToString(), run.GateEvaluation?.DeploymentApproved, run.ErrorMessage);

    public static TestResultDto ToDto(this TestResult r) => new(
        r.Id, r.Name, r.Status, r.DurationMs, r.ErrorMessage, r.StackTrace, r.ExecutedAt, r.MetricsJson,
        r.Evidences.Select(e => new EvidenceDto(e.Id, e.Type, e.FilePath, e.ContentType, e.SizeBytes)).ToList());
}

// ─────────────────────────── Iniciar ejecución ───────────────────────────

public record StartTestRunCommand(
    Guid ProjectId, TestType RunType, EnvironmentType Environment,
    string? CommitSha = null, int? PullRequestNumber = null, Guid? VersionId = null)
    : IRequest<Result<TestRunDto>>;

public class StartTestRunCommandValidator : AbstractValidator<StartTestRunCommand>
{
    public StartTestRunCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.RunType).IsInEnum();
        RuleFor(x => x.Environment).IsInEnum();
    }
}

public class StartTestRunCommandHandler : IRequestHandler<StartTestRunCommand, Result<TestRunDto>>
{
    private readonly ITestRunRepository _runs;
    private readonly IProjectRepository _projects;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly IUnitOfWork _uow;

    public StartTestRunCommandHandler(ITestRunRepository runs, IProjectRepository projects,
        ICurrentUserService currentUser, IBackgroundJobScheduler scheduler, IUnitOfWork uow)
    {
        _runs = runs;
        _projects = projects;
        _currentUser = currentUser;
        _scheduler = scheduler;
        _uow = uow;
    }

    public async Task<Result<TestRunDto>> Handle(StartTestRunCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);
        if (!project.IsActive)
            return Result<TestRunDto>.Failure("El proyecto está inactivo; no se pueden ejecutar pruebas.");

        var run = new TestRun(project.Id, request.RunType, request.Environment,
            _currentUser.Email ?? "system", request.VersionId, request.CommitSha, request.PullRequestNumber);

        await _runs.AddAsync(run, ct);
        await _uow.SaveChangesAsync(ct);

        _scheduler.EnqueueTestRunExecution(run.Id);
        return Result<TestRunDto>.Success(run.ToDto());
    }
}

// ─────────────────────── Ejecutar (job en segundo plano) ───────────────────────

public record ExecuteTestRunCommand(Guid TestRunId) : IRequest<Result<TestRunDto>>;

/// <summary>
/// Orquesta el ciclo completo: ejecutar runner → persistir resultados y evidencias →
/// evaluar quality gate → notificar → analizar fallos con IA → registrar defectos →
/// publicar check en GitHub cuando la ejecución proviene de un Pull Request.
/// </summary>
public class ExecuteTestRunCommandHandler : IRequestHandler<ExecuteTestRunCommand, Result<TestRunDto>>
{
    private readonly ITestRunRepository _runs;
    private readonly IProjectRepository _projects;
    private readonly ITestCaseRepository _testCases;
    private readonly IQualityGateRepository _gates;
    private readonly IDefectRepository _defects;
    private readonly IRepository<SecurityFinding> _findings;
    private readonly IRepository<AiAnalysis> _analyses;
    private readonly IRepository<IntegrationSetting> _integrations;
    private readonly ITestRunnerFactory _runnerFactory;
    private readonly IEvidenceStorage _storage;
    private readonly INotificationDispatcher _notifications;
    private readonly IRunProgressNotifier _progress;
    private readonly IAiAnalysisService _ai;
    private readonly IGitHubClient _gitHub;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ExecuteTestRunCommandHandler> _logger;

    public ExecuteTestRunCommandHandler(
        ITestRunRepository runs, IProjectRepository projects, ITestCaseRepository testCases,
        IQualityGateRepository gates, IDefectRepository defects,
        IRepository<SecurityFinding> findings, IRepository<AiAnalysis> analyses,
        IRepository<IntegrationSetting> integrations,
        ITestRunnerFactory runnerFactory, IEvidenceStorage storage,
        INotificationDispatcher notifications, IRunProgressNotifier progress,
        IAiAnalysisService ai, IGitHubClient gitHub, IUnitOfWork uow,
        ILogger<ExecuteTestRunCommandHandler> logger)
    {
        _runs = runs;
        _projects = projects;
        _testCases = testCases;
        _gates = gates;
        _defects = defects;
        _findings = findings;
        _analyses = analyses;
        _integrations = integrations;
        _runnerFactory = runnerFactory;
        _storage = storage;
        _notifications = notifications;
        _progress = progress;
        _ai = ai;
        _gitHub = gitHub;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<TestRunDto>> Handle(ExecuteTestRunCommand request, CancellationToken ct)
    {
        var run = await _runs.GetWithResultsAsync(request.TestRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.TestRunId);
        var project = await _projects.GetByIdAsync(run.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), run.ProjectId);

        run.Start();
        await _uow.SaveChangesAsync(ct);
        await _progress.RunStatusChangedAsync(run.Id, run.Status.ToString(), ct);

        try
        {
            var outcome = await ExecuteRunnerAsync(run, ct);
            PersistResults(run, outcome);
            await PersistSecurityFindingsAsync(run, outcome, ct);
            run.Complete(outcome.AggregateMetricsJson);

            var evaluation = await EvaluateQualityGateAsync(run, project, ct);
            await _uow.SaveChangesAsync(ct);

            await NotifyAsync(run, project, evaluation, ct);
            await AnalyzeFailuresWithAiAsync(run, project, ct);
            await PublishGitHubCheckAsync(run, project, evaluation, ct);
            await _uow.SaveChangesAsync(ct);

            await _progress.RunCompletedAsync(run.Id, run.Passed, run.Failed,
                evaluation?.Status.ToString() ?? "N/A", ct);
            return Result<TestRunDto>.Success(run.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la ejecución del run {RunId}", run.Id);
            run.MarkFailed(ex.Message);
            await _uow.SaveChangesAsync(ct);
            await _progress.RunStatusChangedAsync(run.Id, run.Status.ToString(), ct);
            return Result<TestRunDto>.Failure($"La ejecución falló: {ex.Message}");
        }
    }

    private async Task<RunnerOutcome> ExecuteRunnerAsync(TestRun run, CancellationToken ct)
    {
        var automated = await _testCases.GetAutomatedByProjectAsync(run.ProjectId, run.RunType, ct);
        var scripts = automated
            .Where(tc => tc.AutomationScriptPath is not null)
            .Select(tc => new TestScriptRef(tc.Id, tc.Title, tc.AutomationScriptPath!))
            .ToList();

        var runner = _runnerFactory.ResolveByTestType(run.RunType);
        var workDir = _storage.CreateRunDirectory(run.Id);
        var parameters = await BuildRunParametersAsync(run, ct);
        var context = new TestRunContext(run.Id, run.ProjectId, run.RunType, run.Environment,
            workDir, scripts, parameters);
        return await runner.ExecuteAsync(context, ct);
    }

    /// <summary>Parámetros específicos del motor (p. ej. el environment de Postman importado).</summary>
    private async Task<Dictionary<string, string>> BuildRunParametersAsync(TestRun run, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>();
        if (run.RunType == TestType.Api)
        {
            var postman = (await _integrations.ListAsync(
                s => s.ProjectId == run.ProjectId && s.Type == IntegrationType.Postman && s.IsEnabled, ct))
                .FirstOrDefault();
            if (postman?.ExtraJson is { } extra)
            {
                try
                {
                    var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(extra);
                    if (values is not null && values.TryGetValue("postmanEnvironment", out var env))
                        parameters["postmanEnvironment"] = env;
                }
                catch (System.Text.Json.JsonException)
                {
                    // ExtraJson malformado: se ejecuta sin environment.
                }
            }
        }
        return parameters;
    }

    private static void PersistResults(TestRun run, RunnerOutcome outcome)
    {
        foreach (var item in outcome.Results)
        {
            var result = run.AddResult(item.Name, item.Status, item.DurationMs,
                item.TestCaseId, item.ErrorMessage, item.StackTrace, item.MetricsJson);
            foreach (var evidence in item.Evidences)
                result.AttachEvidence(evidence.Type, evidence.FilePath, evidence.ContentType, evidence.SizeBytes);
        }
    }

    private async Task PersistSecurityFindingsAsync(TestRun run, RunnerOutcome outcome, CancellationToken ct)
    {
        foreach (var f in outcome.SecurityFindings)
            await _findings.AddAsync(new SecurityFinding(run.Id, f.Name, f.Risk, f.Category,
                f.Url, f.Parameter, f.Evidence, f.Solution, f.CweId), ct);
    }

    private async Task<QualityGateEvaluation?> EvaluateQualityGateAsync(TestRun run, Project project, CancellationToken ct)
    {
        var gateId = project.QualityGateId;
        var gate = gateId.HasValue
            ? await _gates.GetWithConditionsAsync(gateId.Value, ct)
            : await _gates.GetDefaultAsync(ct);
        if (gate is null) return null;

        var criticalFindings = await _findings.CountAsync(
            f => f.TestRunId == run.Id && f.Risk == RiskLevel.Critical, ct);
        var highFindings = await _findings.CountAsync(
            f => f.TestRunId == run.Id && f.Risk == RiskLevel.High, ct);

        var metrics = new Dictionary<GateMetric, decimal>
        {
            [GateMetric.PassRatePercent] = run.PassRatePercent,
            [GateMetric.CriticalVulnerabilities] = criticalFindings,
            [GateMetric.HighVulnerabilities] = highFindings
        };

        var evaluation = gate.Evaluate(run.Id, metrics);
        run.AttachGateEvaluation(evaluation);
        return evaluation;
    }

    private async Task NotifyAsync(TestRun run, Project project, QualityGateEvaluation? evaluation, CancellationToken ct)
    {
        if (run.Failed > 0)
            await _notifications.DispatchAsync(new NotificationMessage(
                NotificationEvents.TestFailed,
                $"❌ Pruebas fallidas en {project.Name}",
                $"Ejecución {run.RunType}: {run.Failed} de {run.TotalTests} pruebas fallaron ({run.PassRatePercent}% éxito).",
                project.Id, null), ct);

        if (evaluation is { Status: QualityGateStatus.Failed })
            await _notifications.DispatchAsync(new NotificationMessage(
                NotificationEvents.DeploymentRejected,
                $"🚫 Despliegue rechazado: {project.Name}",
                "El quality gate no fue superado. El despliegue queda bloqueado hasta corregir los incumplimientos.",
                project.Id, null), ct);

        var hasVulnerabilities = await _findings.AnyAsync(
            f => f.TestRunId == run.Id && f.Risk >= RiskLevel.High, ct);
        if (hasVulnerabilities)
            await _notifications.DispatchAsync(new NotificationMessage(
                NotificationEvents.VulnerabilityFound,
                $"⚠️ Vulnerabilidades detectadas en {project.Name}",
                "El escaneo de seguridad detectó vulnerabilidades de riesgo alto o crítico. Revise el detalle en QA Guardian.",
                project.Id, null), ct);
    }

    private async Task AnalyzeFailuresWithAiAsync(TestRun run, Project project, CancellationToken ct)
    {
        var systemUserId = Guid.Empty; // usuario "system" para defectos auto-registrados
        foreach (var failure in run.Results.Where(r => r.Status == ResultStatus.Failed).Take(10))
        {
            try
            {
                var diagnosis = await _ai.AnalyzeFailureAsync(new FailureContext(
                    failure.Name, failure.ErrorMessage, failure.StackTrace, null, null,
                    failure.Evidences.FirstOrDefault(e => e.Type == EvidenceType.Screenshot)?.FilePath,
                    run.RunType, project.Name), ct);

                await _analyses.AddAsync(new AiAnalysis(
                    failure.Id, null, diagnosis.Diagnosis, diagnosis.ProbableCause,
                    diagnosis.Criticality, diagnosis.Recommendation, diagnosis.SuggestedPriority,
                    diagnosis.EstimatedHours, diagnosis.SuggestedOwnerRole, diagnosis.ModelUsed), ct);

                // Registro automático de defecto para fallos críticos o altos.
                if (diagnosis.Criticality >= RiskLevel.High)
                {
                    var code = await _defects.NextCodeAsync(project.Id, ct);
                    var defect = new Defect(project.Id, code,
                        $"[Auto] {failure.Name}",
                        $"Defecto registrado automáticamente por QA Guardian.\n\nDiagnóstico IA: {diagnosis.Diagnosis}\n" +
                        $"Causa probable: {diagnosis.ProbableCause}\nRecomendación: {diagnosis.Recommendation}",
                        diagnosis.Criticality == RiskLevel.Critical ? DefectSeverity.Critical : DefectSeverity.Major,
                        diagnosis.SuggestedPriority, systemUserId, null, failure.Id,
                        stackTrace: failure.StackTrace);
                    await _defects.AddAsync(defect, ct);
                    await _notifications.DispatchAsync(new NotificationMessage(
                        NotificationEvents.DefectCreated,
                        $"🐞 Defecto automático {code} en {project.Name}",
                        $"{failure.Name}: {diagnosis.Diagnosis}", project.Id, null), ct);
                }
            }
            catch (Exception ex)
            {
                // El análisis IA es best-effort: un fallo del agente no debe invalidar la ejecución.
                _logger.LogWarning(ex, "Análisis IA falló para el resultado {ResultId}", failure.Id);
            }
        }
    }

    private async Task PublishGitHubCheckAsync(TestRun run, Project project,
        QualityGateEvaluation? evaluation, CancellationToken ct)
    {
        if (run.CommitSha is null) return;
        try
        {
            var approved = evaluation?.DeploymentApproved ?? run.Failed == 0;
            var conclusion = approved ? "success" : "failure";
            var summary = $"Pruebas: {run.Passed}/{run.TotalTests} exitosas ({run.PassRatePercent}%). " +
                          $"Quality Gate: {evaluation?.Status.ToString() ?? "sin gate"}.";

            await _gitHub.CreateCheckRunAsync(project.Id, run.CommitSha,
                $"QA Guardian / {run.RunType}", "completed", conclusion,
                approved ? "Quality Gate superado" : "Quality Gate NO superado", summary, ct);

            if (run.PullRequestNumber.HasValue)
                await _gitHub.CommentOnPullRequestAsync(project.Id, run.PullRequestNumber.Value,
                    $"## 🛡️ QA Guardian — {run.RunType}\n\n" +
                    $"| Métrica | Valor |\n|---|---|\n" +
                    $"| Total | {run.TotalTests} |\n| ✅ Exitosas | {run.Passed} |\n" +
                    $"| ❌ Fallidas | {run.Failed} |\n| ⏭️ Omitidas | {run.Skipped} |\n" +
                    $"| % Éxito | {run.PassRatePercent}% |\n| Quality Gate | **{evaluation?.Status.ToString() ?? "N/A"}** |\n\n" +
                    (approved ? "✅ **Despliegue aprobado.**" : "🚫 **Despliegue bloqueado.**"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo publicar el check de GitHub para el run {RunId}", run.Id);
        }
    }
}

// ─────────────────────────── Cancelar ───────────────────────────

public record CancelTestRunCommand(Guid TestRunId) : IRequest<Result<bool>>;

public class CancelTestRunCommandHandler : IRequestHandler<CancelTestRunCommand, Result<bool>>
{
    private readonly ITestRunRepository _runs;
    private readonly IUnitOfWork _uow;

    public CancelTestRunCommandHandler(ITestRunRepository runs, IUnitOfWork uow)
    {
        _runs = runs;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(CancelTestRunCommand request, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(request.TestRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.TestRunId);
        run.Cancel();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetTestRunsQuery(Guid ProjectId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<TestRunDto>>;

public class GetTestRunsQueryHandler : IRequestHandler<GetTestRunsQuery, PagedResult<TestRunDto>>
{
    private readonly ITestRunRepository _runs;

    public GetTestRunsQueryHandler(ITestRunRepository runs) => _runs = runs;

    public async Task<PagedResult<TestRunDto>> Handle(GetTestRunsQuery request, CancellationToken ct)
    {
        var (items, total) = await _runs.PagedAsync(request.Page, request.PageSize,
            r => r.ProjectId == request.ProjectId && !r.IsDeleted, ct);
        return new PagedResult<TestRunDto>(
            items.OrderByDescending(r => r.CreatedAt).Select(r => r.ToDto()).ToList(),
            total, request.Page, request.PageSize);
    }
}

public record GetTestRunDetailQuery(Guid Id) : IRequest<(TestRunDto Run, IReadOnlyList<TestResultDto> Results)>;

public class GetTestRunDetailQueryHandler
    : IRequestHandler<GetTestRunDetailQuery, (TestRunDto Run, IReadOnlyList<TestResultDto> Results)>
{
    private readonly ITestRunRepository _runs;

    public GetTestRunDetailQueryHandler(ITestRunRepository runs) => _runs = runs;

    public async Task<(TestRunDto, IReadOnlyList<TestResultDto>)> Handle(
        GetTestRunDetailQuery request, CancellationToken ct)
    {
        var run = await _runs.GetWithResultsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.Id);
        return (run.ToDto(), run.Results.Select(r => r.ToDto()).ToList());
    }
}
