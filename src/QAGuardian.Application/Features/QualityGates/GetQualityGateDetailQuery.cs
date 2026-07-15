using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.QualityGates;

public record GetQualityGateDetailQuery(Guid Id) : IRequest<QualityGateDetailDto>;

public class GetQualityGateDetailQueryHandler : IRequestHandler<GetQualityGateDetailQuery, QualityGateDetailDto>
{
    private readonly IQualityGateRepository _gates;

    public GetQualityGateDetailQueryHandler(IQualityGateRepository gates) => _gates = gates;

    public async Task<QualityGateDetailDto> Handle(GetQualityGateDetailQuery request, CancellationToken ct)
    {
        var gate = await _gates.GetWithConditionsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(QualityGate), request.Id);

        var dto = gate.ToDto();
        return new QualityGateDetailDto(dto.Id, dto.Name, dto.IsDefault, dto.Conditions);
    }
}

public record QualityGateDetailDto(
    Guid Id,
    string Name,
    bool IsDefault,
    IReadOnlyList<GateConditionDto> Conditions
);
