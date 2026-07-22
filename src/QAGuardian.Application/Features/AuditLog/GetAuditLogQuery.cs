using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.Application.Features.AuditLog;

/// <summary>
/// Consulta el historial de auditoría (ISO 27001 A.12.4) registrado por <c>AuditMiddleware</c>.
/// El middleware guarda la ruta HTTP completa en <see cref="Domain.Entities.AuditLog.EntityName"/>
/// (no hay un EntityId separado), por lo que el filtro busca coincidencia parcial en la ruta.
/// B6 / ADR-013: no-Admin solo ve filas cuya ruta contenga un GUID de proyecto accesible.
/// </summary>
public sealed record GetAuditLogQuery(
    string? PathContains = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class GetAuditLogQueryHandler(
    IRepository<Domain.Entities.AuditLog> auditRepo,
    IProjectAccessService access)
    : IRequestHandler<GetAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var pathFilter = request.PathContains;

        if (access.IsGlobalAdministrator())
        {
            var (adminItems, adminTotal) = await auditRepo.PagedAsync(
                request.Page,
                request.PageSize,
                l => pathFilter == null || l.EntityName.Contains(pathFilter),
                ct);

            return ToPage(adminItems, adminTotal, request);
        }

        var accessibleIds = await access.ListAccessibleProjectIdsAsync(ct);
        var idTexts = accessibleIds.Select(id => id.ToString()).ToArray();
        if (idTexts.Length == 0)
            return new PagedResult<AuditLogEntryDto>([], 0, request.Page, request.PageSize);

        // EF Core traduce Contains sobre colección local + string.Contains.
        var (items, total) = await auditRepo.PagedAsync(
            request.Page,
            request.PageSize,
            l => (pathFilter == null || l.EntityName.Contains(pathFilter))
                 && idTexts.Any(id => l.EntityName.Contains(id)),
            ct);

        return ToPage(items, total, request);
    }

    private static PagedResult<AuditLogEntryDto> ToPage(
        List<Domain.Entities.AuditLog> items, int total, GetAuditLogQuery request)
    {
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
