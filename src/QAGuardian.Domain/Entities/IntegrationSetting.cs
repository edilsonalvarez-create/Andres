using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Configuración de una integración externa por proyecto (SonarQube, GitHub, ZAP, etc.).</summary>
public class IntegrationSetting : AuditableEntity
{
    private IntegrationSetting() { } // EF Core

    public IntegrationSetting(Guid projectId, IntegrationType type, string baseUrl,
        string? encryptedToken, string? extraJson)
    {
        ProjectId = projectId;
        Type = type;
        BaseUrl = baseUrl?.Trim() ?? string.Empty;
        EncryptedToken = encryptedToken;
        ExtraJson = extraJson;
        IsEnabled = true;
    }

    public Guid ProjectId { get; private set; }
    public IntegrationType Type { get; private set; }
    public string BaseUrl { get; private set; } = default!;
    /// <summary>Token/credencial cifrado en reposo (ISO 27001 A.10).</summary>
    public string? EncryptedToken { get; private set; }
    /// <summary>Parámetros adicionales serializados (org, projectKey, repo, etc.).</summary>
    public string? ExtraJson { get; private set; }
    public bool IsEnabled { get; private set; }

    public void Update(string baseUrl, string? encryptedToken, string? extraJson, bool isEnabled)
    {
        BaseUrl = baseUrl?.Trim() ?? string.Empty;
        if (encryptedToken is not null) EncryptedToken = encryptedToken;
        ExtraJson = extraJson;
        IsEnabled = isEnabled;
    }
}
