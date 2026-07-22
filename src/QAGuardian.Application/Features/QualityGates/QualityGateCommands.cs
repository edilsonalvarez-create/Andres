using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.QualityGates;

public record GateConditionDto(Guid Id, GateMetric Metric, GateOperator Operator, decimal Threshold, bool IsBlocking);
public record QualityGateDto(Guid Id, string Name, bool IsDefault, IReadOnlyList<GateConditionDto> Conditions);

public static class QualityGateMapper
{
    public static QualityGateDto ToDto(this QualityGate g) => new(
        g.Id, g.Name, g.IsDefault,
        g.Conditions.Select(c => new GateConditionDto(c.Id, c.Metric, c.Operator, c.Threshold, c.IsBlocking)).ToList());
}

public record ConditionInput(GateMetric Metric, GateOperator Operator, decimal Threshold, bool IsBlocking = true);

public record CreateQualityGateCommand(string Name, bool IsDefault, List<ConditionInput> Conditions)
    : IRequest<Result<QualityGateDto>>;

public class CreateQualityGateCommandValidator : AbstractValidator<CreateQualityGateCommand>
{
    public CreateQualityGateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Conditions).NotEmpty().WithMessage("El gate debe tener al menos una condición.");
    }
}

public class CreateQualityGateCommandHandler : IRequestHandler<CreateQualityGateCommand, Result<QualityGateDto>>
{
    private readonly IQualityGateRepository _gates;
    private readonly IUnitOfWork _uow;

    public CreateQualityGateCommandHandler(IQualityGateRepository gates, IUnitOfWork uow)
    {
        _gates = gates;
        _uow = uow;
    }

    public async Task<Result<QualityGateDto>> Handle(CreateQualityGateCommand request, CancellationToken ct)
    {
        if (request.IsDefault)
        {
            var currentDefault = await _gates.GetDefaultAsync(ct);
            currentDefault?.UnmarkDefault();
        }

        var gate = new QualityGate(request.Name, request.IsDefault);
        foreach (var c in request.Conditions)
            gate.AddCondition(c.Metric, c.Operator, c.Threshold, c.IsBlocking);

        await _gates.AddAsync(gate, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<QualityGateDto>.Success(gate.ToDto());
    }
}

public record GetQualityGatesQuery() : IRequest<IReadOnlyList<QualityGateDto>>;

public class GetQualityGatesQueryHandler : IRequestHandler<GetQualityGatesQuery, IReadOnlyList<QualityGateDto>>
{
    private readonly IQualityGateRepository _gates;

    public GetQualityGatesQueryHandler(IQualityGateRepository gates) => _gates = gates;

    public async Task<IReadOnlyList<QualityGateDto>> Handle(GetQualityGatesQuery request, CancellationToken ct)
    {
        var gates = await _gates.ListWithConditionsAsync(ct);
        return gates.Select(g => g.ToDto()).ToList();
    }
}

public record AssignGateToProjectCommand(Guid ProjectId, Guid QualityGateId) : IRequest<Result<bool>>;

public class AssignGateToProjectCommandHandler : IRequestHandler<AssignGateToProjectCommand, Result<bool>>
{
    private readonly IProjectRepository _projects;
    private readonly IQualityGateRepository _gates;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public AssignGateToProjectCommandHandler(
        IProjectRepository projects, IQualityGateRepository gates,
        IProjectAccessService access, IUnitOfWork uow)
    {
        _projects = projects;
        _gates = gates;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(AssignGateToProjectCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAdministerProjectAsync(request.ProjectId, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);
        _ = await _gates.GetByIdAsync(request.QualityGateId, ct)
            ?? throw new NotFoundException(nameof(QualityGate), request.QualityGateId);
        project.AssignQualityGate(request.QualityGateId);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
