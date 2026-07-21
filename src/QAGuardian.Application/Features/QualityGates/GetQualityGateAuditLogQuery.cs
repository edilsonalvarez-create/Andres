using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Application.Features.AuditLog;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.QualityGates;

/// <summary>Historial de auditoría de un quality gate (B6 / ADR-013: solo Administrador global).</summary>
public sealed record GetQualityGateAuditLogQuery(Guid QualityGateId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class GetQualityGateAuditLogQueryHandler(
    IRepository<Domain.Entities.AuditLog> auditRepo,
    IProjectAccessService access)
    : IRequestHandler<GetQualityGateAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetQualityGateAuditLogQuery request, CancellationToken ct)
    {
        if (!access.IsGlobalAdministrator())
            throw new Domain.Common.NotFoundException(nameof(QualityGate), request.QualityGateId);

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
