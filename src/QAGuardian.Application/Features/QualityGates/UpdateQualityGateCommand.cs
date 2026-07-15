using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.QualityGates;

public record UpdateQualityGateCommand(
    Guid Id,
    string Name,
    List<ConditionInput> Conditions
) : IRequest<Result<QualityGateDto>>;

public class UpdateQualityGateCommandValidator : AbstractValidator<UpdateQualityGateCommand>
{
    public UpdateQualityGateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Conditions).NotEmpty().WithMessage("El gate debe tener al menos una condición.");
    }
}

public class UpdateQualityGateCommandHandler : IRequestHandler<UpdateQualityGateCommand, Result<QualityGateDto>>
{
    private readonly IQualityGateRepository _gates;
    private readonly IUnitOfWork _uow;

    public UpdateQualityGateCommandHandler(IQualityGateRepository gates, IUnitOfWork uow)
    {
        _gates = gates;
        _uow = uow;
    }

    public async Task<Result<QualityGateDto>> Handle(UpdateQualityGateCommand request, CancellationToken ct)
    {
        var gate = await _gates.GetWithConditionsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(QualityGate), request.Id);

        // Actualizar nombre
        gate.Rename(request.Name);

        // Actualizar condiciones: eliminar todas y agregar nuevamente
        var existingConditionIds = gate.Conditions.Select(c => c.Id).ToList();
        foreach (var condId in existingConditionIds)
            gate.RemoveCondition(condId);

        foreach (var c in request.Conditions)
            gate.AddCondition(c.Metric, c.Operator, c.Threshold, c.IsBlocking);

        await _uow.SaveChangesAsync(ct);
        return Result<QualityGateDto>.Success(gate.ToDto());
    }
}
