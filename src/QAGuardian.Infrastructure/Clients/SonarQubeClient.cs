using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Clients;

/// <summary>Cliente de la API de SonarQube: métricas de cobertura, duplicación, bugs y vulnerabilidades.</summary>
public class SonarQubeClient : ISonarQubeClient
{
    private readonly HttpClient _http;
    private readonly IntegrationSettingResolver _resolver;
    private readonly ILogger<SonarQubeClient> _logger;

    public SonarQubeClient(HttpClient http, IntegrationSettingResolver resolver, ILogger<SonarQubeClient> logger)
    {
        _http = http;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<SonarMetricsDto?> GetMetricsAsync(Guid projectId, string projectKey, CancellationToken ct = default)
    {
        var integration = await _resolver.ResolveAsync(projectId, IntegrationType.SonarQube, ct);
        if (integration is null)
        {
            _logger.LogInformation("SonarQube no está configurado para el proyecto {ProjectId}", projectId);
            return null;
        }

        var key = string.IsNullOrEmpty(projectKey)
            ? integration.Extra.GetValueOrDefault("projectKey", "")
            : projectKey;
        if (string.IsNullOrEmpty(key)) return null;

        const string metricKeys = "coverage,duplicated_lines_density,bugs,vulnerabilities,security_hotspots,code_smells";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{integration.BaseUrl}/api/measures/component?component={Uri.EscapeDataString(key)}&metricKeys={metricKeys}");
        AddAuth(request, integration.Token);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SonarQube respondió {Status} para el proyecto {Key}", response.StatusCode, key);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var measures = doc.RootElement.GetProperty("component").GetProperty("measures");
        var values = new Dictionary<string, string>();
        foreach (var measure in measures.EnumerateArray())
            values[measure.GetProperty("metric").GetString()!] =
                measure.TryGetProperty("value", out var v) ? v.GetString() ?? "0" : "0";

        var gateStatus = await GetQualityGateStatusAsync(integration, key, ct);

        return new SonarMetricsDto(
            key,
            ParseDecimal(values.GetValueOrDefault("coverage")),
            ParseDecimal(values.GetValueOrDefault("duplicated_lines_density")),
            ParseInt(values.GetValueOrDefault("bugs")),
            ParseInt(values.GetValueOrDefault("vulnerabilities")),
            ParseInt(values.GetValueOrDefault("security_hotspots")),
            ParseInt(values.GetValueOrDefault("code_smells")),
            gateStatus);
    }

    private async Task<string> GetQualityGateStatusAsync(ResolvedIntegration integration, string key, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{integration.BaseUrl}/api/qualitygates/project_status?projectKey={Uri.EscapeDataString(key)}");
            AddAuth(request, integration.Token);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return "UNKNOWN";
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("projectStatus").GetProperty("status").GetString() ?? "UNKNOWN";
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private static void AddAuth(HttpRequestMessage request, string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        // SonarQube usa Basic auth con el token como usuario y contraseña vacía.
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{token}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static int ParseInt(string? value) => int.TryParse(value, out var i) ? i : 0;
}
