using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.DatabaseValidation;
using QAGuardian.Application.Features.TestRuns;

namespace QAGuardian.Infrastructure.Jobs;

/// <summary>Encola trabajos en Hangfire para su ejecución en segundo plano.</summary>
public class HangfireJobScheduler : IBackgroundJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireJobScheduler(IBackgroundJobClient jobs) => _jobs = jobs;

    public string EnqueueTestRunExecution(Guid testRunId)
        => _jobs.Enqueue<TestExecutionJob>(job => job.ExecuteAsync(testRunId, CancellationToken.None));

    public string EnqueueDatabaseValidation(
        Guid validationRunId, Guid projectId, string sourceEnvironment, string targetEnvironment)
        => _jobs.Enqueue<TestExecutionJob>(job =>
            job.ValidateDatabaseAsync(
                validationRunId, projectId, sourceEnvironment, targetEnvironment, CancellationToken.None));
}

/// <summary>Job de Hangfire: delega en MediatR la ejecución del run o la validación de BD.</summary>
public class TestExecutionJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<TestExecutionJob> _logger;

    public TestExecutionJob(IMediator mediator, ILogger<TestExecutionJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync(Guid testRunId, CancellationToken ct)
    {
        _logger.LogInformation("Iniciando ejecución en segundo plano del run {RunId}", testRunId);
        await _mediator.Send(new ExecuteTestRunCommand(testRunId), ct);
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ValidateDatabaseAsync(
        Guid validationRunId, Guid projectId, string sourceEnvironment, string targetEnvironment,
        CancellationToken ct)
    {
        // Args Hangfire inspectables sin secretos: solo ids y nombres de entorno.
        _logger.LogInformation(
            "Iniciando validación de BD {ValidationId} proyecto {ProjectId} {Source}→{Target}",
            validationRunId, projectId, sourceEnvironment, targetEnvironment);
        await _mediator.Send(new ExecuteDatabaseValidationCommand(validationRunId), ct);
    }
}
