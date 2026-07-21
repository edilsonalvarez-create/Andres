using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Clients;

/// <summary>Prueba credenciales de integraciones externas antes de guardarlas (SonarQube, GitHub, ZAP).</summary>
public class IntegrationConnectionTester : IIntegrationConnectionTester
{
    /// <summary>
    /// Named client CON SsrfOutboundHandler + primary handler endurecido (B3).
    /// Se reutiliza el registrado en DependencyInjection para no crear otro pipeline.
    /// </summary>
    internal const string SsrfGuardedClientName = "notifications";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISsrfGuard _ssrf;
    private readonly ILogger<IntegrationConnectionTester> _logger;

    public IntegrationConnectionTester(
        IHttpClientFactory httpClientFactory,
        ISsrfGuard ssrf,
        ILogger<IntegrationConnectionTester> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ssrf = ssrf;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(
        IntegrationType type, string baseUrl, string? token, CancellationToken ct = default)
    {
        try
        {
            return type switch
            {
                IntegrationType.SonarQube => await TestSonarQubeAsync(baseUrl, token, ct),
                IntegrationType.GitHub => await TestGitHubAsync(token, ct),
                IntegrationType.OwaspZap => TestOwaspZapBaseUrl(baseUrl),
                IntegrationType.JMeter or IntegrationType.Postman or IntegrationType.Playwright
                    or IntegrationType.SqlServer => true, // No requieren prueba de conexión HTTP
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al probar conexión de integración {IntegrationType}", type);
            return false;
        }
    }

    private bool TestOwaspZapBaseUrl(string baseUrl)
    {
        var check = _ssrf.ValidateOutboundUri(baseUrl);
        if (!check.IsSuccess)
        {
            _logger.LogWarning("BaseUrl ZAP bloqueada por SSRF: {Reason}", check.Error);
            return false;
        }

        return true;
    }

    private async Task<bool> TestSonarQubeAsync(string baseUrl, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var check = _ssrf.ValidateOutboundUri(baseUrl);
        if (!check.IsSuccess)
        {
            _logger.LogWarning("BaseUrl SonarQube bloqueada por SSRF: {Reason}", check.Error);
            return false;
        }

        var safeBase = check.Value!.ToString().TrimEnd('/');
        var client = _httpClientFactory.CreateClient(SsrfGuardedClientName);
        var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{token}:"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        // Re-validar URI completa del probe (path + host).
        var probe = $"{safeBase}/api/ce/info";
        var probeCheck = _ssrf.ValidateOutboundUri(probe);
        if (!probeCheck.IsSuccess)
            return false;

        var response = await client.GetAsync(probeCheck.Value, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> TestGitHubAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Destino fijo de plataforma — no es BaseUrl de proyecto.
        var client = _httpClientFactory.CreateClient(SsrfGuardedClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QAGuardian");

        var response = await client.GetAsync("https://api.github.com/user", ct);
        return response.IsSuccessStatusCode;
    }
}
