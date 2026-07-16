using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Approvals;

public record ApprovalRequestDto(
    Guid Id, Guid ProjectId, ApprovalType Type, Guid TargetEntityId,
    string Title, string? Comment, ApprovalStatus Status,
    Guid RequestedByUserId, Guid? DecidedByUserId,
    DateTime? DecidedAt, string? DecisionComment, DateTime CreatedAt);

public static class ApprovalMapper
{
    public static ApprovalRequestDto ToDto(this ApprovalRequest a) => new(
        a.Id, a.ProjectId, a.Type, a.TargetEntityId, a.Title, a.Comment, a.Status,
        a.RequestedByUserId, a.DecidedByUserId, a.DecidedAt, a.DecisionComment, a.CreatedAt);
}

public record CreateApprovalRequestCommand(
    Guid ProjectId, ApprovalType Type, Guid TargetEntityId, string Title, string? Comment)
    : IRequest<Result<ApprovalRequestDto>>;

public class CreateApprovalRequestCommandValidator : AbstractValidator<CreateApprovalRequestCommand>
{
    public CreateApprovalRequestCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.TargetEntityId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public class CreateApprovalRequestCommandHandler
    : IRequestHandler<CreateApprovalRequestCommand, Result<ApprovalRequestDto>>
{
    private readonly IRepository<ApprovalRequest> _approvals;
    private readonly IProjectRepository _projects;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public CreateApprovalRequestCommandHandler(
        IRepository<ApprovalRequest> approvals, IProjectRepository projects,
        ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _approvals = approvals;
        _projects = projects;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<ApprovalRequestDto>> Handle(
        CreateApprovalRequestCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<ApprovalRequestDto>.Failure("Usuario no autenticado.");

        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var pendingExists = await _approvals.AnyAsync(a =>
            a.TargetEntityId == request.TargetEntityId
            && a.Type == request.Type
            && a.Status == ApprovalStatus.Pending
            && !a.IsDeleted, ct);
        if (pendingExists)
            return Result<ApprovalRequestDto>.Failure("Ya existe una solicitud pendiente para este elemento.");

        var entity = new ApprovalRequest(
            request.ProjectId, request.Type, request.TargetEntityId,
            request.Title, _currentUser.UserId.Value, request.Comment);
        await _approvals.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ApprovalRequestDto>.Success(entity.ToDto());
    }
}

public record GetPendingApprovalsQuery(Guid? ProjectId = null)
    : IRequest<IReadOnlyList<ApprovalRequestDto>>;

public class GetPendingApprovalsQueryHandler
    : IRequestHandler<GetPendingApprovalsQuery, IReadOnlyList<ApprovalRequestDto>>
{
    private readonly IRepository<ApprovalRequest> _approvals;

    public GetPendingApprovalsQueryHandler(IRepository<ApprovalRequest> approvals)
        => _approvals = approvals;

    public async Task<IReadOnlyList<ApprovalRequestDto>> Handle(
        GetPendingApprovalsQuery request, CancellationToken ct)
    {
        var items = await _approvals.ListAsync(a =>
            !a.IsDeleted
            && a.Status == ApprovalStatus.Pending
            && (request.ProjectId == null || a.ProjectId == request.ProjectId), ct);
        return items.OrderByDescending(a => a.CreatedAt).Select(a => a.ToDto()).ToList();
    }
}

public record DecideApprovalCommand(Guid Id, bool Approve, string? DecisionComment)
    : IRequest<Result<ApprovalRequestDto>>;

public class DecideApprovalCommandValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DecideApprovalCommandHandler
    : IRequestHandler<DecideApprovalCommand, Result<ApprovalRequestDto>>
{
    private readonly IRepository<ApprovalRequest> _approvals;
    private readonly ITestCaseRepository _testCases;
    private readonly IRepository<ProjectVersion> _versions;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public DecideApprovalCommandHandler(
        IRepository<ApprovalRequest> approvals,
        ITestCaseRepository testCases,
        IRepository<ProjectVersion> versions,
        ICurrentUserService currentUser,
        IUnitOfWork uow)
    {
        _approvals = approvals;
        _testCases = testCases;
        _versions = versions;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<ApprovalRequestDto>> Handle(
        DecideApprovalCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<ApprovalRequestDto>.Failure("Usuario no autenticado.");

        var canDecide = _currentUser.IsInRole(SystemRoles.Administrator)
                        || _currentUser.IsInRole(SystemRoles.TechLead)
                        || _currentUser.IsInRole(SystemRoles.ProductOwner);
        if (!canDecide)
            return Result<ApprovalRequestDto>.Failure(
                "Solo LiderTecnico, ProductOwner o Administrador pueden decidir aprobaciones.");

        var approval = await _approvals.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(ApprovalRequest), request.Id);

        if (request.Approve)
        {
            approval.Approve(_currentUser.UserId.Value, request.DecisionComment);
            await ApplySideEffectAsync(approval, ct);
        }
        else
        {
            approval.Reject(_currentUser.UserId.Value, request.DecisionComment);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<ApprovalRequestDto>.Success(approval.ToDto());
    }

    private async Task ApplySideEffectAsync(ApprovalRequest approval, CancellationToken ct)
    {
        switch (approval.Type)
        {
            case ApprovalType.TestCaseActivation:
            {
                var tc = await _testCases.GetByIdAsync(approval.TargetEntityId, ct);
                tc?.Activate();
                break;
            }
            case ApprovalType.VersionRelease:
            {
                var version = await _versions.GetByIdAsync(approval.TargetEntityId, ct);
                version?.MarkReleased();
                break;
            }
            // GateOverride: la decisión queda registrada; el override operativo lo aplica Quality Gates.
            case ApprovalType.GateOverride:
                break;
        }
    }
}
