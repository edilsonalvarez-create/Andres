using MediatR;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.TestCases;

// ─────────────────── Ejecutar un caso de prueba individual ("probar") ───────────────────

/// <summary>Resultado individual de una prueba dentro de la ejecución.</summary>
public record SingleRunItemDto(
    string Name, string Status, long DurationMs, string? ErrorMessage, string? MetricsJson);

/// <summary>Resultado de ejecutar un único caso de prueba desde el editor.</summary>
public record SingleRunResultDto(
    bool Succeeded,
    string Framework,
    int Total,
    int Passed,
    int Failed,
    IReadOnlyList<SingleRunItemDto> Items,
    string? ErrorMessage);

/// <summary>
/// Ejecuta un único caso de prueba automatizado con el runner de su framework y devuelve
/// el resultado en línea (sin registrar una corrida formal). Pensado para el botón
/// "Guardar y ejecutar" del editor: correr el script recién escrito y ver si funciona.
/// </summary>
public record RunTestCaseCommand(Guid TestCaseId, EnvironmentType Environment = EnvironmentType.QA)
    : IRequest<Result<SingleRunResultDto>>;

public class RunTestCaseCommandHandler : IRequestHandler<RunTestCaseCommand, Result<SingleRunResultDto>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly ITestRunnerFactory _runnerFactory;
    private readonly IEvidenceStorage _storage;
    private readonly IProjectAccessService _access;
    private readonly ILogger<RunTestCaseCommandHandler> _logger;

    public RunTestCaseCommandHandler(
        ITestCaseRepository testCases, ITestRunnerFactory runnerFactory,
        IEvidenceStorage storage, IProjectAccessService access,
        ILogger<RunTestCaseCommandHandler> logger)
    {
        _testCases = testCases;
        _runnerFactory = runnerFactory;
        _storage = storage;
        _access = access;
        _logger = logger;
    }

    public async Task<Result<SingleRunResultDto>> Handle(RunTestCaseCommand request, CancellationToken ct)
    {
        var testCase = await _testCases.GetByIdAsync(request.TestCaseId, ct);
        if (testCase is null || !await _access.CanAccessProjectAsync(testCase.ProjectId, ct))
            throw new NotFoundException(nameof(TestCase), request.TestCaseId);

        if (testCase.Framework == AutomationFramework.Manual || testCase.AutomationScriptPath is null)
            return Result<SingleRunResultDto>.Failure(
                "El caso no tiene un script automatizado que ejecutar. Guarde el script primero.");

        ITestRunner runner;
        try
        {
            runner = _runnerFactory.Resolve(testCase.Framework);
        }
        catch (NotSupportedException)
        {
            return Result<SingleRunResultDto>.Failure(
                $"Aún no hay un motor de ejecución para el framework {testCase.Framework}.");
        }

        var workDir = _storage.CreateRunDirectory(Guid.NewGuid());
        var scripts = new List<TestScriptRef>
        {
            new(testCase.Id, testCase.Title, testCase.AutomationScriptPath!)
        };
        var context = new TestRunContext(Guid.NewGuid(), testCase.ProjectId, testCase.Type,
            request.Environment, workDir, scripts, new Dictionary<string, string>());

        try
        {
            var outcome = await runner.ExecuteAsync(context, ct);

            var items = outcome.Results
                .Select(r => new SingleRunItemDto(r.Name, r.Status.ToString(), r.DurationMs,
                    r.ErrorMessage, r.MetricsJson))
                .ToList();
            var passed = outcome.Results.Count(r => r.Status == ResultStatus.Passed);
            var failed = outcome.Results.Count(r => r.Status == ResultStatus.Failed);

            return Result<SingleRunResultDto>.Success(new SingleRunResultDto(
                outcome.Succeeded, testCase.Framework.ToString(),
                items.Count, passed, failed, items, outcome.ErrorMessage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la ejecución individual del caso {TestCaseId}", testCase.Id);
            return Result<SingleRunResultDto>.Success(new SingleRunResultDto(
                false, testCase.Framework.ToString(), 0, 0, 0, [],
                $"No se pudo ejecutar la prueba: {ex.Message}"));
        }
    }
}
