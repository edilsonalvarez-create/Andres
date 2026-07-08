using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Registro de auditoría inmutable (ISO 27001 A.12.4).</summary>
public class AuditLog : BaseEntity
{
    private AuditLog() { } // EF Core

    public AuditLog(Guid? userId, string userEmail, string action, string entityName,
        string? entityId, string? oldValues, string? newValues, string? ipAddress)
    {
        UserId = userId;
        UserEmail = userEmail;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        Timestamp = DateTime.UtcNow;
    }

    public Guid? UserId { get; private set; }
    public string UserEmail { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string EntityName { get; private set; } = default!;
    public string? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime Timestamp { get; private set; }
}
