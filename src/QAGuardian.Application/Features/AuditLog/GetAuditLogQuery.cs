using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.Application.Features.AuditLog;

/// <summary>
/// Consulta el historial de auditoría (ISO 27001 A.12.4) registrado por <c>AuditMiddleware</c>.
/// El middleware guarda la ruta HTTP completa en <see cref="Domain.Entities.AuditLog.EntityName"/>
/// (no hay un EntityId separado), por lo que el filtro busca coincidencia parcial en la ruta.
/// </summary>
public sealed record GetAuditLogQuery(
    string? PathContains = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class GetAuditLogQueryHandler(IRepository<Domain.Entities.AuditLog> auditRepo)
    : IRequestHandler<GetAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var (items, total) = await auditRepo.PagedAsync(
            request.Page,
            request.PageSize,
            l => request.PathContains == null || l.EntityName.Contains(request.PathContains),
            ct);

        var dtos = items
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditLogEntryDto(
                l.Id, l.UserEmail, l.Action, l.EntityName, l.Timestamp, l.IpAddress))
            .ToList();

        return new PagedResult<AuditLogEntryDto>(dtos, total, request.Page, request.PageSize);
    }
}

public sealed record AuditLogEntryDto(
    Guid Id,
    string UserEmail,
    string Action,
    string EntityName,
    DateTime Timestamp,
    string? IpAddress
);
