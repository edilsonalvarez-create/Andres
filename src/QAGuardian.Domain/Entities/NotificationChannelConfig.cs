using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Configuración de un canal de notificación (correo, Teams, Slack, Discord, Telegram).</summary>
public class NotificationChannelConfig : AuditableEntity
{
    private NotificationChannelConfig() { } // EF Core

    public NotificationChannelConfig(Guid? projectId, NotificationChannel channel,
        string target, NotificationEvents events)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new DomainException("El destino del canal (webhook, correo o chat id) es obligatorio.");
        ProjectId = projectId;
        Channel = channel;
        Target = target.Trim();
        Events = events;
        IsEnabled = true;
    }

    /// <summary>Null = configuración global.</summary>
    public Guid? ProjectId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    /// <summary>URL de webhook, dirección de correo o chat id según el canal.</summary>
    public string Target { get; private set; } = default!;
    public NotificationEvents Events { get; private set; }
    public bool IsEnabled { get; private set; }

    public bool ListensTo(NotificationEvents evt) => IsEnabled && Events.HasFlag(evt);

    public void Update(string target, NotificationEvents events, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new DomainException("El destino del canal es obligatorio.");
        Target = target.Trim();
        Events = events;
        IsEnabled = isEnabled;
    }
}
