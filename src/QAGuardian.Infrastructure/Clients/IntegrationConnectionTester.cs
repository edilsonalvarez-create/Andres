using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Clients;

/// <summary>Prueba credenciales de integraciones externas antes de guardarlas (SonarQube, GitHub, ZAP).</summary>
public class IntegrationConnectionTester : IIntegrationConnectionTester
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntegrationConnectionTester> _logger;

    public IntegrationConnectionTester(IHttpClientFactory httpClientFactory, ILogger<IntegrationConnectionTester> logger)
    {
        _httpClientFactory = httpClientFactory;
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
                IntegrationType.OwaspZap => Uri.TryCreate(baseUrl, UriKind.Absolute, out _),
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

    private async Task<bool> TestSonarQubeAsync(string baseUrl, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var client = _httpClientFactory.CreateClient();
        var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{token}:"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/ce/info", ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> TestGitHubAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QAGuardian");

        var response = await client.GetAsync("https://api.github.com/user", ct);
        return response.IsSuccessStatusCode;
    }
}
