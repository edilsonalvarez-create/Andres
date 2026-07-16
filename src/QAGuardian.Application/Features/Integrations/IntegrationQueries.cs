using System.Text.Json;
using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Integrations;

// ─────────────────────────── SonarQube ───────────────────────────

public record GetSonarMetricsQuery(Guid ProjectId) : IRequest<SonarMetricsDto?>;

public class GetSonarMetricsQueryHandler : IRequestHandler<GetSonarMetricsQuery, SonarMetricsDto?>
{
    private readonly ISonarQubeClient _sonar;
    private readonly IRepository<IntegrationSetting> _settings;

    public GetSonarMetricsQueryHandler(ISonarQubeClient sonar, IRepository<IntegrationSetting> settings)
    {
        _sonar = sonar;
        _settings = settings;
    }

    public async Task<SonarMetricsDto?> Handle(GetSonarMetricsQuery request, CancellationToken ct)
    {
        var settings = await _settings.ListAsync(
            s => s.ProjectId == request.ProjectId && s.Type == IntegrationType.SonarQube && s.IsEnabled, ct);
        var setting = settings.FirstOrDefault();
        if (setting is null) return null;

        var extra = string.IsNullOrEmpty(setting.ExtraJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(setting.ExtraJson) ?? [];
        var projectKey = extra.GetValueOrDefault("projectKey", "");
        return await _sonar.GetMetricsAsync(request.ProjectId, projectKey, ct);
    }
}

// ─────────────────────────── GitHub ───────────────────────────

public record GetGitHubPullRequestsQuery(Guid ProjectId, string State = "open")
    : IRequest<IReadOnlyList<GitHubPullRequestDto>>;

public class GetGitHubPullRequestsQueryHandler
    : IRequestHandler<GetGitHubPullRequestsQuery, IReadOnlyList<GitHubPullRequestDto>>
{
    private readonly IGitHubClient _gitHub;

    public GetGitHubPullRequestsQueryHandler(IGitHubClient gitHub) => _gitHub = gitHub;

    public Task<IReadOnlyList<GitHubPullRequestDto>> Handle(
        GetGitHubPullRequestsQuery request, CancellationToken ct)
        => _gitHub.GetPullRequestsAsync(request.ProjectId, request.State, ct);
}

// ─────────────── Agente inteligente: análisis de Pull Request ───────────────

public record PrAnalysisDto(
    int PullRequestNumber,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> ImpactedAreas,
    IReadOnlyList<GeneratedTestCase> GeneratedTests,
    Guid? SmokeRunId,
    string Summary);

/// <summary>
/// Analiza un PR: detecta archivos modificados, genera casos de prueba con IA para las
/// áreas impactadas, dispara los Smoke Tests y comenta el resultado en el PR.
/// </summary>
public record AnalyzePullRequestCommand(Guid ProjectId, int PullRequestNumber)
    : IRequest<Result<PrAnalysisDto>>;

public class AnalyzePullRequestCommandValidator : AbstractValidator<AnalyzePullRequestCommand>
{
    public AnalyzePullRequestCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.PullRequestNumber).GreaterThan(0);
    }
}

public class AnalyzePullRequestCommandHandler : IRequestHandler<AnalyzePullRequestCommand, Result<PrAnalysisDto>>
{
    private readonly IProjectRepository _projects;
    private readonly ITestCaseRepository _testCases;
    private readonly ITestRunRepository _runs;
    private readonly IGitHubClient _gitHub;
    private readonly IAiTestGenerationService _testGen;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public AnalyzePullRequestCommandHandler(
        IProjectRepository projects, ITestCaseRepository testCases, ITestRunRepository runs,
        IGitHubClient gitHub, IAiTestGenerationService testGen,
        IBackgroundJobScheduler scheduler, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _projects = projects;
        _testCases = testCases;
        _runs = runs;
        _gitHub = gitHub;
        _testGen = testGen;
        _scheduler = scheduler;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<PrAnalysisDto>> Handle(AnalyzePullRequestCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var changedFiles = await _gitHub.GetPullRequestChangedFilesAsync(
            project.Id, request.PullRequestNumber, ct);
        if (changedFiles.Count == 0)
            return Result<PrAnalysisDto>.Failure("El Pull Request no contiene archivos modificados.");

        // Grounding Sprint 7: diff real + catálogo existente (evita duplicados/alucinaciones).
        var diffExcerpt = await _gitHub.GetPullRequestDiffExcerptAsync(
            project.Id, request.PullRequestNumber, ct: ct);
        var catalog = await _testCases.ListAsync(tc => tc.ProjectId == project.Id, ct);
        var catalogTitles = catalog
            .OrderByDescending(tc => tc.CreatedAt)
            .Take(25)
            .Select(tc => $"{tc.Code}: {tc.Title}")
            .ToList();

        // 1. Generación de casos de prueba con IA para los archivos impactados.
        var generated = await _testGen.GenerateTestsForChangesAsync(
            new TestGenerationRequest(project.Name, changedFiles, diffExcerpt, catalogTitles), ct);

        // 2. Persistir los casos generados como borradores (incluye script sugerido como paso).
        var sequence = await _testCases.CountAsync(tc => tc.ProjectId == project.Id, ct);
        foreach (var gen in generated.TestCases)
        {
            sequence++;
            var code = $"TC-AI-{sequence:D4}";
            var testCase = new TestCase(project.Id, code, gen.Title,
                gen.Framework == AutomationFramework.Postman ? TestType.Api : TestType.Functional,
                TestPriority.High,
                preconditions: $"Generado por IA para PR #{request.PullRequestNumber}. {gen.Rationale}");
            if (!string.IsNullOrWhiteSpace(gen.SuggestedScript))
                testCase.AddStep(1, gen.SuggestedScript, "Script sugerido por IA — revisar antes de automatizar.");
            await _testCases.AddAsync(testCase, ct);
        }

        // 3. Ejecutar Smoke Tests del proyecto (siempre) + pruebas impactadas.
        var prs = await _gitHub.GetPullRequestsAsync(project.Id, "open", ct);
        var pr = prs.FirstOrDefault(p => p.Number == request.PullRequestNumber);
        var smokeRun = new TestRun(project.Id, TestType.Smoke, EnvironmentType.QA,
            _currentUser.Email ?? "pr-agent", commitSha: pr?.HeadSha,
            pullRequestNumber: request.PullRequestNumber);
        await _runs.AddAsync(smokeRun, ct);
        await _uow.SaveChangesAsync(ct);
        _scheduler.EnqueueTestRunExecution(smokeRun.Id);

        // 4. Comentario resumen en el PR.
        var summary = $"Archivos modificados: {changedFiles.Count}. " +
                      $"Áreas impactadas: {string.Join(", ", generated.ImpactedAreas)}. " +
                      $"Casos de prueba generados: {generated.TestCases.Count}. Smoke Tests encolados.";
        await _gitHub.CommentOnPullRequestAsync(project.Id, request.PullRequestNumber,
            $"## 🤖 QA Guardian — Análisis automático del PR\n\n{summary}\n\n" +
            string.Join("\n", generated.TestCases.Select(t => $"- **{t.Title}** ({t.Framework}): {t.Rationale}")), ct);

        return Result<PrAnalysisDto>.Success(new PrAnalysisDto(
            request.PullRequestNumber, changedFiles, generated.ImpactedAreas,
            generated.TestCases, smokeRun.Id, summary));
    }
}

// ─────────────────────────── Configurar integraciones ───────────────────────────

public record UpsertIntegrationCommand(
    Guid ProjectId, IntegrationType Type, string BaseUrl, string? Token, string? ExtraJson, bool IsEnabled)
    : IRequest<Result<Guid>>;

public class UpsertIntegrationCommandValidator : AbstractValidator<UpsertIntegrationCommand>
{
    public UpsertIntegrationCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.BaseUrl).NotEmpty().Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("La URL base no es válida.");
    }
}

public class UpsertIntegrationCommandHandler : IRequestHandler<UpsertIntegrationCommand, Result<Guid>>
{
    private readonly IRepository<IntegrationSetting> _settings;
    private readonly ITokenEncryptionService _encryption;
    private readonly IUnitOfWork _uow;

    public UpsertIntegrationCommandHandler(IRepository<IntegrationSetting> settings,
        ITokenEncryptionService encryption, IUnitOfWork uow)
    {
        _settings = settings;
        _encryption = encryption;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(UpsertIntegrationCommand request, CancellationToken ct)
    {
        var encryptedToken = string.IsNullOrEmpty(request.Token) ? null : _encryption.Encrypt(request.Token);
        var existing = (await _settings.ListAsync(
            s => s.ProjectId == request.ProjectId && s.Type == request.Type, ct)).FirstOrDefault();

        if (existing is not null)
        {
            existing.Update(request.BaseUrl, encryptedToken, request.ExtraJson, request.IsEnabled);
            await _uow.SaveChangesAsync(ct);
            return Result<Guid>.Success(existing.Id);
        }

        var setting = new IntegrationSetting(request.ProjectId, request.Type,
            request.BaseUrl, encryptedToken, request.ExtraJson);
        await _settings.AddAsync(setting, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(setting.Id);
    }
}
