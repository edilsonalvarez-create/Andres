using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;
using QAGuardian.Application.Features.AuditLog;

namespace QAGuardian.Application.Features.QualityGates;

/// <summary>Historial de auditoría de un quality gate específico (busca por coincidencia de ruta,
/// ver <see cref="GetAuditLogQueryHandler"/>).</summary>
public sealed record GetQualityGateAuditLogQuery(Guid QualityGateId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class GetQualityGateAuditLogQueryHandler(IRepository<Domain.Entities.AuditLog> auditRepo)
    : IRequestHandler<GetQualityGateAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetQualityGateAuditLogQuery request, CancellationToken ct)
    {
        var idText = request.QualityGateId.ToString();
        var (items, total) = await auditRepo.PagedAsync(
            request.Page,
            request.PageSize,
            l => l.EntityName.Contains(idText),
            ct);

        var dtos = items
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditLogEntryDto(
                l.Id, l.UserEmail, l.Action, l.EntityName, l.Timestamp, l.IpAddress))
            .ToList();

        return new PagedResult<AuditLogEntryDto>(dtos, total, request.Page, request.PageSize);
    }
}
