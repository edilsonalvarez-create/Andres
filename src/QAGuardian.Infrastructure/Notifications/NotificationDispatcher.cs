using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Persistence;

namespace QAGuardian.Infrastructure.Notifications;

/// <summary>
/// Despacha notificaciones a todos los canales configurados (correo, Teams, Slack,
/// Discord, Telegram) que estén suscritos al evento. Cada canal es best-effort:
/// el fallo de uno no impide los demás.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly QAGuardianDbContext _context;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(QAGuardianDbContext context, IHttpClientFactory httpFactory,
        IConfiguration configuration, ILogger<NotificationDispatcher> logger)
    {
        _context = context;
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var channels = await _context.NotificationChannels
            .Where(c => !c.IsDeleted && c.IsEnabled
                && (c.ProjectId == null || c.ProjectId == message.ProjectId))
            .ToListAsync(ct);

        foreach (var channel in channels.Where(c => c.ListensTo(message.Event)))
        {
            try
            {
                await (channel.Channel switch
                {
                    NotificationChannel.Email => SendEmailAsync(channel.Target, message, ct),
                    NotificationChannel.MicrosoftTeams => SendTeamsAsync(channel.Target, message, ct),
                    NotificationChannel.Slack => SendSlackAsync(channel.Target, message, ct),
                    NotificationChannel.Discord => SendDiscordAsync(channel.Target, message, ct),
                    NotificationChannel.Telegram => SendTelegramAsync(channel.Target, message, ct),
                    _ => Task.CompletedTask
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallo notificando por {Channel} a {Target}",
                    channel.Channel, channel.Target);
            }
        }
    }

    private async Task SendEmailAsync(string to, NotificationMessage message, CancellationToken ct)
    {
        var host = _configuration["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            _logger.LogInformation("SMTP no configurado; se omite el correo a {To}", to);
            return;
        }

        using var client = new SmtpClient(host, _configuration.GetValue("Smtp:Port", 587))
        {
            EnableSsl = _configuration.GetValue("Smtp:UseSsl", true),
            Credentials = new NetworkCredential(
                _configuration["Smtp:User"], _configuration["Smtp:Password"])
        };
        using var mail = new MailMessage(
            _configuration["Smtp:From"] ?? "qaguardian@localhost", to,
            message.Title,
            $"{message.Body}\n\n{(message.LinkUrl is null ? "" : $"Detalle: {message.LinkUrl}")}\n\n— QA Guardian");
        await client.SendMailAsync(mail, ct);
    }

    private Task SendTeamsAsync(string webhookUrl, NotificationMessage message, CancellationToken ct)
        => PostJsonAsync(webhookUrl, new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", size = "Medium", weight = "Bolder", text = message.Title },
                            new { type = "TextBlock", wrap = true, text = message.Body }
                        }
                    }
                }
            }
        }, ct);

    private Task SendSlackAsync(string webhookUrl, NotificationMessage message, CancellationToken ct)
        => PostJsonAsync(webhookUrl, new { text = $"*{message.Title}*\n{message.Body}" }, ct);

    private Task SendDiscordAsync(string webhookUrl, NotificationMessage message, CancellationToken ct)
        => PostJsonAsync(webhookUrl, new { content = $"**{message.Title}**\n{message.Body}" }, ct);

    private async Task SendTelegramAsync(string target, NotificationMessage message, CancellationToken ct)
    {
        // Target esperado: "<botToken>|<chatId>"
        var parts = target.Split('|', 2);
        if (parts.Length != 2)
        {
            _logger.LogWarning("Canal Telegram mal configurado; se espera 'botToken|chatId'.");
            return;
        }
        await PostJsonAsync($"https://api.telegram.org/bot{parts[0]}/sendMessage",
            new { chat_id = parts[1], text = $"{message.Title}\n\n{message.Body}" }, ct);
    }

    private async Task PostJsonAsync(string url, object payload, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("notifications");
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, ct);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("Webhook {Url} respondió {Status}", url, response.StatusCode);
    }
}
