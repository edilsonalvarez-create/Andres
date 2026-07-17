using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
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

public record ProjectDatabaseEnvironmentDto(Guid Id, Guid ProjectId, string Name, bool IsActive);

// ─────────────────────────── Iniciar validación ───────────────────────────

/// <summary>
/// Body de API: solo nombres de entorno. Cualquier clave de connection string → 400 (Sprint 12).
/// </summary>
public sealed class StartDatabaseValidationRequest
{
    public Guid ProjectId { get; init; }
    public string SourceEnvironment { get; init; } = "";
    public string TargetEnvironment { get; init; } = "";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public StartDatabaseValidationCommand ToCommand()
    {
        RejectClientSecrets();
        return new StartDatabaseValidationCommand(ProjectId, SourceEnvironment, TargetEnvironment);
    }

    private void RejectClientSecrets()
    {
        if (ExtensionData is null || ExtensionData.Count == 0)
            return;

        var forbidden = ExtensionData.Keys
            .Where(IsForbiddenClientSecretKey)
            .ToList();
        if (forbidden.Count == 0)
            return;

        var failures = forbidden.Select(k => new ValidationFailure(k,
            "No se aceptan connection strings del cliente. Use entornos nombrados configurados en el servidor."));
        throw new Common.Exceptions.ValidationException(failures);
    }

    internal static bool IsForbiddenClientSecretKey(string key)
    {
        var n = key.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return n.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
               || n.Equals("sourceconn", StringComparison.OrdinalIgnoreCase)
               || n.Equals("targetconn", StringComparison.OrdinalIgnoreCase)
               || n.Equals("conn", StringComparison.OrdinalIgnoreCase)
               || n.Equals("connection", StringComparison.OrdinalIgnoreCase);
    }
}

public record StartDatabaseValidationCommand(
    Guid ProjectId, string SourceEnvironment, string TargetEnvironment) : IRequest<Result<Guid>>;

public class StartDatabaseValidationCommandValidator : AbstractValidator<StartDatabaseValidationCommand>
{
    private static readonly Regex EnvNameRegex = new(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,49}$", RegexOptions.Compiled);

    public StartDatabaseValidationCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.SourceEnvironment)
            .NotEmpty()
            .Must(BeSafeEnvironmentName)
            .WithMessage("SourceEnvironment debe ser un nombre de entorno (ej. 'dev'), no una connection string.");
        RuleFor(x => x.TargetEnvironment)
            .NotEmpty()
            .Must(BeSafeEnvironmentName)
            .WithMessage("TargetEnvironment debe ser un nombre de entorno (ej. 'staging'), no una connection string.");
        RuleFor(x => x)
            .Must(x => !string.Equals(
                ProjectDatabaseEnvironment.NormalizeName(x.SourceEnvironment),
                ProjectDatabaseEnvironment.NormalizeName(x.TargetEnvironment),
                StringComparison.Ordinal))
            .WithMessage("Source y Target deben ser entornos distintos.");
    }

    private static bool BeSafeEnvironmentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (LooksLikeConnectionString(name))
            return false;
        return EnvNameRegex.IsMatch(name.Trim());
    }

    internal static bool LooksLikeConnectionString(string value)
    {
        var v = value.Trim();
        return v.Contains(';', StringComparison.Ordinal)
               || v.Contains("Password=", StringComparison.OrdinalIgnoreCase)
               || v.Contains("Pwd=", StringComparison.OrdinalIgnoreCase)
               || v.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
               || v.Contains("Server=", StringComparison.OrdinalIgnoreCase)
               || v.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
               || v.Contains("User Id=", StringComparison.OrdinalIgnoreCase);
    }
}

public class StartDatabaseValidationCommandHandler
    : IRequestHandler<StartDatabaseValidationCommand, Result<Guid>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly IProjectDatabaseConnectionResolver _resolver;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public StartDatabaseValidationCommandHandler(
        IRepository<DatabaseValidationRun> validations,
        IBackgroundJobScheduler scheduler,
        IProjectDatabaseConnectionResolver resolver,
        IProjectAccessService access,
        IUnitOfWork uow)
    {
        _validations = validations;
        _scheduler = scheduler;
        _resolver = resolver;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(StartDatabaseValidationCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);

        var source = ProjectDatabaseEnvironment.NormalizeName(request.SourceEnvironment);
        var target = ProjectDatabaseEnvironment.NormalizeName(request.TargetEnvironment);

        if (!await _resolver.ExistsAsync(request.ProjectId, source, ct))
            return Result<Guid>.Failure($"Entorno de BD '{source}' no configurado para este proyecto.");
        if (!await _resolver.ExistsAsync(request.ProjectId, target, ct))
            return Result<Guid>.Failure($"Entorno de BD '{target}' no configurado para este proyecto.");

        var run = new DatabaseValidationRun(request.ProjectId, source, target);
        await _validations.AddAsync(run, ct);
        await _uow.SaveChangesAsync(ct);

        _scheduler.EnqueueDatabaseValidation(run.Id, request.ProjectId, source, target);
        return Result<Guid>.Success(run.Id);
    }
}

// ─────────────────── Ejecutar comparación (job en segundo plano) ───────────────────

public record ExecuteDatabaseValidationCommand(Guid ValidationRunId) : IRequest<Result<bool>>;

public class ExecuteDatabaseValidationCommandHandler
    : IRequestHandler<ExecuteDatabaseValidationCommand, Result<bool>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;
    private readonly IProjectDatabaseConnectionResolver _resolver;
    private readonly IDatabaseSchemaValidator _validator;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ExecuteDatabaseValidationCommandHandler> _logger;

    public ExecuteDatabaseValidationCommandHandler(
        IRepository<DatabaseValidationRun> validations,
        IProjectDatabaseConnectionResolver resolver,
        IDatabaseSchemaValidator validator,
        IUnitOfWork uow,
        ILogger<ExecuteDatabaseValidationCommandHandler> logger)
    {
        _validations = validations;
        _resolver = resolver;
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
            var sourceConn = await _resolver.ResolveAsync(run.ProjectId, run.SourceEnvironment, ct);
            var targetConn = await _resolver.ResolveAsync(run.ProjectId, run.TargetEnvironment, ct);

            var differences = await _validator.CompareAsync(sourceConn, targetConn, ct);
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

// ─────────────────────────── Entornos (config server-side) ───────────────────────────

public record UpsertProjectDatabaseEnvironmentCommand(
    Guid ProjectId, string Name, string ConnectionString) : IRequest<Result<Guid>>;

public class UpsertProjectDatabaseEnvironmentCommandValidator
    : AbstractValidator<UpsertProjectDatabaseEnvironmentCommand>
{
    private static readonly Regex EnvNameRegex = new(@"^[a-zA-Z0-9][a-zA-Z0-9._-]{0,49}$", RegexOptions.Compiled);

    public UpsertProjectDatabaseEnvironmentCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(n => EnvNameRegex.IsMatch(n.Trim()))
            .WithMessage("Name debe ser un identificador corto (ej. 'dev', 'staging').");
        RuleFor(x => x.ConnectionString).NotEmpty().MinimumLength(10);
    }
}

public class UpsertProjectDatabaseEnvironmentCommandHandler
    : IRequestHandler<UpsertProjectDatabaseEnvironmentCommand, Result<Guid>>
{
    private readonly IProjectDatabaseEnvironmentRepository _envs;
    private readonly ITokenEncryptionService _encryption;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public UpsertProjectDatabaseEnvironmentCommandHandler(
        IProjectDatabaseEnvironmentRepository envs,
        ITokenEncryptionService encryption,
        IProjectAccessService access,
        IUnitOfWork uow)
    {
        _envs = envs;
        _encryption = encryption;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(UpsertProjectDatabaseEnvironmentCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);

        var name = ProjectDatabaseEnvironment.NormalizeName(request.Name);
        var encrypted = _encryption.Encrypt(request.ConnectionString.Trim());
        var existing = await _envs.GetByNameAsync(request.ProjectId, name, ct);
        if (existing is null)
        {
            var created = new ProjectDatabaseEnvironment(request.ProjectId, name, encrypted);
            await _envs.AddAsync(created, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<Guid>.Success(created.Id);
        }

        existing.UpdateEncryptedConnectionString(encrypted);
        existing.Activate();
        _envs.Update(existing);
        await _uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(existing.Id);
    }
}

public record GetProjectDatabaseEnvironmentsQuery(Guid ProjectId)
    : IRequest<IReadOnlyList<ProjectDatabaseEnvironmentDto>>;

public class GetProjectDatabaseEnvironmentsQueryHandler
    : IRequestHandler<GetProjectDatabaseEnvironmentsQuery, IReadOnlyList<ProjectDatabaseEnvironmentDto>>
{
    private readonly IProjectDatabaseEnvironmentRepository _envs;
    private readonly IProjectAccessService _access;

    public GetProjectDatabaseEnvironmentsQueryHandler(
        IProjectDatabaseEnvironmentRepository envs, IProjectAccessService access)
    {
        _envs = envs;
        _access = access;
    }

    public async Task<IReadOnlyList<ProjectDatabaseEnvironmentDto>> Handle(
        GetProjectDatabaseEnvironmentsQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        var items = await _envs.ListActiveByProjectAsync(request.ProjectId, ct);
        return items
            .Select(e => new ProjectDatabaseEnvironmentDto(e.Id, e.ProjectId, e.Name, e.IsActive))
            .ToList();
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetDatabaseValidationsQuery(Guid ProjectId) : IRequest<IReadOnlyList<DatabaseValidationDto>>;

public class GetDatabaseValidationsQueryHandler
    : IRequestHandler<GetDatabaseValidationsQuery, IReadOnlyList<DatabaseValidationDto>>
{
    private readonly IRepository<DatabaseValidationRun> _validations;
    private readonly IProjectAccessService _access;

    public GetDatabaseValidationsQueryHandler(
        IRepository<DatabaseValidationRun> validations, IProjectAccessService access)
    {
        _validations = validations;
        _access = access;
    }

    public async Task<IReadOnlyList<DatabaseValidationDto>> Handle(
        GetDatabaseValidationsQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        var items = await _validations.ListAsync(v => v.ProjectId == request.ProjectId && !v.IsDeleted, ct);
        return items.OrderByDescending(v => v.StartedAt)
            .Select(v => new DatabaseValidationDto(v.Id, v.ProjectId, v.SourceEnvironment,
                v.TargetEnvironment, v.Status.ToString(), v.StartedAt, v.CompletedAt,
                v.DifferencesCount, v.DifferencesJson, v.ErrorMessage))
            .ToList();
    }
}
