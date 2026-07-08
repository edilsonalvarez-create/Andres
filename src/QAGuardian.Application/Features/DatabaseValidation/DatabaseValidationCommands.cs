using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.DatabaseValidation;

public record DatabaseValidationDto(
    Guid Id, Guid ProjectId, string SourceEnvironment, string TargetEnvironment,
    string Status, DateTime StartedAt, DateTime? CompletedAt,
    int DifferencesCount, string? DifferencesJson, string? ErrorMessage);

// ─────────────────────────── Iniciar validación ───────────────────────────

public record StartDatabaseValidationCommand(
    Guid ProjectId, string SourceEnvironment, string SourceConnectionString,
    string TargetEnvironment, string TargetConnectionString) : IRequest<Result<Guid>>;

public class StartDatabaseValidationCommandValidator : AbstractValidator<StartDatabaseValidationCommand>
{
    public StartDatabaseValidationCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.SourceEnvironment).NotEmpty();
        RuleFor(x => x.SourceConnectionString).NotEmpty();
        RuleFor(x => x.TargetEnvironment).NotEmpty();
        RuleFor(x => x.TargetConnectionString).NotEmpty();
    }
}

public class StartDatabaseValidationCommandHandler
    : IRequestHandler<StartDatabaseValidationCommand, Result<Guid>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly IUnitOfWork _uow;

    public StartDatabaseValidationCommandHandler(IRepository<DatabaseValidationRun> validations,
        IBackgroundJobScheduler scheduler, IUnitOfWork uow)
    {
        _validations = validations;
        _scheduler = scheduler;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(StartDatabaseValidationCommand request, CancellationToken ct)
    {
        var run = new DatabaseValidationRun(request.ProjectId,
            request.SourceEnvironment, request.TargetEnvironment);
        await _validations.AddAsync(run, ct);
        await _uow.SaveChangesAsync(ct);

        _scheduler.EnqueueDatabaseValidation(run.Id,
            request.SourceConnectionString, request.TargetConnectionString);
        return Result<Guid>.Success(run.Id);
    }
}

// ─────────────────── Ejecutar comparación (job en segundo plano) ───────────────────

public record ExecuteDatabaseValidationCommand(Guid ValidationRunId,
    string SourceConnectionString, string TargetConnectionString) : IRequest<Result<bool>>;

public class ExecuteDatabaseValidationCommandHandler
    : IRequestHandler<ExecuteDatabaseValidationCommand, Result<bool>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;
    private readonly IDatabaseSchemaValidator _validator;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ExecuteDatabaseValidationCommandHandler> _logger;

    public ExecuteDatabaseValidationCommandHandler(IRepository<DatabaseValidationRun> validations,
        IDatabaseSchemaValidator validator, IUnitOfWork uow,
        ILogger<ExecuteDatabaseValidationCommandHandler> logger)
    {
        _validations = validations;
        _validator = validator;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(ExecuteDatabaseValidationCommand request, CancellationToken ct)
    {
        var run = await _validations.GetByIdAsync(request.ValidationRunId, ct)
            ?? throw new NotFoundException(nameof(DatabaseValidationRun), request.ValidationRunId);
        try
        {
            var differences = await _validator.CompareAsync(
                request.SourceConnectionString, request.TargetConnectionString, ct);
            run.Complete(differences.Count, JsonSerializer.Serialize(differences));
            await _uow.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validación de base de datos {Id} falló", run.Id);
            run.Fail(ex.Message);
            await _uow.SaveChangesAsync(ct);
            return Result<bool>.Failure(ex.Message);
        }
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetDatabaseValidationsQuery(Guid ProjectId) : IRequest<IReadOnlyList<DatabaseValidationDto>>;

public class GetDatabaseValidationsQueryHandler
    : IRequestHandler<GetDatabaseValidationsQuery, IReadOnlyList<DatabaseValidationDto>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;

    public GetDatabaseValidationsQueryHandler(IRepository<DatabaseValidationRun> validations)
        => _validations = validations;

    public async Task<IReadOnlyList<DatabaseValidationDto>> Handle(
        GetDatabaseValidationsQuery request, CancellationToken ct)
    {
        var items = await _validations.ListAsync(v => v.ProjectId == request.ProjectId && !v.IsDeleted, ct);
        return items.OrderByDescending(v => v.StartedAt)
            .Select(v => new DatabaseValidationDto(v.Id, v.ProjectId, v.SourceEnvironment,
                v.TargetEnvironment, v.Status.ToString(), v.StartedAt, v.CompletedAt,
                v.DifferencesCount, v.DifferencesJson, v.ErrorMessage))
            .ToList();
    }
}
