using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Defects;

public record DefectDto(
    Guid Id, Guid ProjectId, Guid? ModuleId, string Code, string Title, string Description,
    DefectSeverity Severity, DefectPriority Priority, DefectStatus Status,
    Guid ReportedByUserId, Guid? AssignedToUserId, string? Sprint, string? Version,
    DateTime CreatedAt, DateTime? ResolvedAt, DateTime? ClosedAt);

public static class DefectMapper
{
    public static DefectDto ToDto(this Defect d) => new(
        d.Id, d.ProjectId, d.ModuleId, d.Code, d.Title, d.Description,
        d.Severity, d.Priority, d.Status, d.ReportedByUserId, d.AssignedToUserId,
        d.Sprint, d.Version, d.CreatedAt, d.ResolvedAt, d.ClosedAt);
}

// ─────────────────────────── Crear defecto ───────────────────────────

public record CreateDefectCommand(
    Guid ProjectId, string Title, string Description, DefectSeverity Severity,
    DefectPriority Priority, Guid? ModuleId, Guid? TestResultId,
    string? Sprint, string? Version, string? StackTrace) : IRequest<Result<DefectDto>>;

public class CreateDefectCommandValidator : AbstractValidator<CreateDefectCommand>
{
    public CreateDefectCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class CreateDefectCommandHandler : IRequestHandler<CreateDefectCommand, Result<DefectDto>>
{
    private readonly IDefectRepository _defects;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationDispatcher _notifications;
    private readonly IUnitOfWork _uow;

    public CreateDefectCommandHandler(IDefectRepository defects, ICurrentUserService currentUser,
        INotificationDispatcher notifications, IUnitOfWork uow)
    {
        _defects = defects;
        _currentUser = currentUser;
        _notifications = notifications;
        _uow = uow;
    }

    public async Task<Result<DefectDto>> Handle(CreateDefectCommand request, CancellationToken ct)
    {
        var code = await _defects.NextCodeAsync(request.ProjectId, ct);
        var defect = new Defect(request.ProjectId, code, request.Title, request.Description,
            request.Severity, request.Priority, _currentUser.UserId ?? Guid.Empty,
            request.ModuleId, request.TestResultId, request.Sprint, request.Version, request.StackTrace);

        await _defects.AddAsync(defect, ct);
        await _uow.SaveChangesAsync(ct);

        await _notifications.DispatchAsync(new NotificationMessage(
            NotificationEvents.DefectCreated,
            $"🐞 Nuevo defecto {code}",
            $"{request.Title} — Severidad: {request.Severity}, Prioridad: {request.Priority}",
            request.ProjectId, null), ct);

        return Result<DefectDto>.Success(defect.ToDto());
    }
}

// ─────────────────────────── Transiciones de estado ───────────────────────────

public record ChangeDefectStatusCommand(Guid Id, DefectStatus TargetStatus, Guid? AssignToUserId = null)
    : IRequest<Result<DefectDto>>;

public class ChangeDefectStatusCommandHandler : IRequestHandler<ChangeDefectStatusCommand, Result<DefectDto>>
{
    private readonly IDefectRepository _defects;
    private readonly IUnitOfWork _uow;

    public ChangeDefectStatusCommandHandler(IDefectRepository defects, IUnitOfWork uow)
    {
        _defects = defects;
        _uow = uow;
    }

    public async Task<Result<DefectDto>> Handle(ChangeDefectStatusCommand request, CancellationToken ct)
    {
        var defect = await _defects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Defect), request.Id);

        switch (request.TargetStatus)
        {
            case DefectStatus.Assigned:
                if (request.AssignToUserId is null)
                    return Result<DefectDto>.Failure("Debe indicar el usuario a asignar.");
                defect.Assign(request.AssignToUserId.Value);
                break;
            case DefectStatus.InProgress: defect.StartProgress(); break;
            case DefectStatus.Resolved: defect.Resolve(); break;
            case DefectStatus.Verified: defect.Verify(); break;
            case DefectStatus.Closed: defect.Close(); break;
            case DefectStatus.Reopened: defect.Reopen(); break;
            case DefectStatus.Rejected: defect.Reject(); break;
            default:
                return Result<DefectDto>.Failure($"Transición no soportada: {request.TargetStatus}.");
        }

        await _uow.SaveChangesAsync(ct);
        return Result<DefectDto>.Success(defect.ToDto());
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetDefectsQuery(Guid ProjectId, int Page = 1, int PageSize = 20,
    DefectStatus? Status = null, DefectSeverity? Severity = null) : IRequest<PagedResult<DefectDto>>;

public class GetDefectsQueryHandler : IRequestHandler<GetDefectsQuery, PagedResult<DefectDto>>
{
    private readonly IDefectRepository _defects;

    public GetDefectsQueryHandler(IDefectRepository defects) => _defects = defects;

    public async Task<PagedResult<DefectDto>> Handle(GetDefectsQuery request, CancellationToken ct)
    {
        var (items, total) = await _defects.PagedAsync(request.Page, request.PageSize,
            d => d.ProjectId == request.ProjectId && !d.IsDeleted
                && (request.Status == null || d.Status == request.Status)
                && (request.Severity == null || d.Severity == request.Severity),
            ct);
        return new PagedResult<DefectDto>(
            items.OrderByDescending(d => d.CreatedAt).Select(d => d.ToDto()).ToList(),
            total, request.Page, request.PageSize);
    }
}
