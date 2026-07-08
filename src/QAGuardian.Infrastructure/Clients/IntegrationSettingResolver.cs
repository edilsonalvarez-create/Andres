using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Persistence;

namespace QAGuardian.Infrastructure.Clients;

public record ResolvedIntegration(string BaseUrl, string? Token, IReadOnlyDictionary<string, string> Extra);

/// <summary>Resuelve y descifra la configuración de integración de un proyecto.</summary>
public class IntegrationSettingResolver
{
    private readonly QAGuardianDbContext _context;
    private readonly ITokenEncryptionService _encryption;

    public IntegrationSettingResolver(QAGuardianDbContext context, ITokenEncryptionService encryption)
    {
        _context = context;
        _encryption = encryption;
    }

    public async Task<ResolvedIntegration?> ResolveAsync(Guid projectId, IntegrationType type, CancellationToken ct)
    {
        var setting = await _context.IntegrationSettings
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Type == type && s.IsEnabled && !s.IsDeleted, ct);
        if (setting is null) return null;

        var token = setting.EncryptedToken is null ? null : _encryption.Decrypt(setting.EncryptedToken);
        var extra = string.IsNullOrEmpty(setting.ExtraJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(setting.ExtraJson) ?? [];
        return new ResolvedIntegration(setting.BaseUrl.TrimEnd('/'), token, extra);
    }
}
