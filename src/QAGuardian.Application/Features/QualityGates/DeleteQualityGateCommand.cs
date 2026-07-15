using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.QualityGates;

public sealed record DeleteQualityGateCommand(Guid Id) : IRequest<Result<bool>>;

public sealed class DeleteQualityGateCommandHandler(IQualityGateRepository gates, IUnitOfWork uow)
    : IRequestHandler<DeleteQualityGateCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteQualityGateCommand request, CancellationToken ct)
    {
        var gate = await gates.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(QualityGate), request.Id);

        gate.IsDeleted = true;

        await uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
