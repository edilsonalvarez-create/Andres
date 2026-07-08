using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Clients;

/// <summary>Cliente de la API REST de GitHub: PRs, checks, comentarios, releases y workflows.</summary>
public class GitHubApiClient : IGitHubClient
{
    private readonly HttpClient _http;
    private readonly IntegrationSettingResolver _resolver;
    private readonly ILogger<GitHubApiClient> _logger;

    public GitHubApiClient(HttpClient http, IntegrationSettingResolver resolver, ILogger<GitHubApiClient> logger)
    {
        _http = http;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GitHubPullRequestDto>> GetPullRequestsAsync(
        Guid projectId, string state = "open", CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var doc = await GetJsonAsync($"repos/{repo}/pulls?state={state}&per_page=50", token, ct);
        if (doc is null) return [];

        var list = new List<GitHubPullRequestDto>();
        foreach (var pr in doc.RootElement.EnumerateArray())
        {
            list.Add(new GitHubPullRequestDto(
                pr.GetProperty("number").GetInt32(),
                pr.GetProperty("title").GetString() ?? "",
                pr.GetProperty("state").GetString() ?? "",
                pr.GetProperty("user").GetProperty("login").GetString() ?? "",
                pr.GetProperty("head").GetProperty("ref").GetString() ?? "",
                pr.GetProperty("base").GetProperty("ref").GetString() ?? "",
                pr.GetProperty("head").GetProperty("sha").GetString() ?? "",
                pr.GetProperty("html_url").GetString() ?? "",
                pr.GetProperty("created_at").GetDateTime(),
                pr.TryGetProperty("mergeable", out var m) && m.ValueKind == JsonValueKind.True));
        }
        return list;
    }

    public async Task<GitHubCheckRunDto> CreateCheckRunAsync(Guid projectId, string headSha, string name,
        string status, string? conclusion, string title, string summary, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        var body = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["head_sha"] = headSha,
            ["status"] = status,
            ["output"] = new { title, summary }
        };
        if (conclusion is not null) body["conclusion"] = conclusion;

        using var doc = await PostJsonAsync($"repos/{repo}/check-runs", body, token, ct)
            ?? throw new InvalidOperationException("GitHub no aceptó la creación del check run.");
        var root = doc.RootElement;
        return new GitHubCheckRunDto(
            root.GetProperty("id").GetInt64(),
            root.GetProperty("name").GetString() ?? name,
            root.GetProperty("status").GetString() ?? status,
            root.TryGetProperty("conclusion", out var c) ? c.GetString() : null,
            root.GetProperty("html_url").GetString() ?? "");
    }

    public async Task CommentOnPullRequestAsync(Guid projectId, int prNumber, string comment, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var _ = await PostJsonAsync($"repos/{repo}/issues/{prNumber}/comments",
            new { body = comment }, token, ct);
    }

    public async Task<IReadOnlyList<string>> GetPullRequestChangedFilesAsync(
        Guid projectId, int prNumber, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var doc = await GetJsonAsync($"repos/{repo}/pulls/{prNumber}/files?per_page=100", token, ct);
        if (doc is null) return [];
        return doc.RootElement.EnumerateArray()
            .Select(f => f.GetProperty("filename").GetString() ?? "")
            .Where(f => f.Length > 0)
            .ToList();
    }

    public async Task CreateReleaseAsync(Guid projectId, string tagName, string name, string body, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var _ = await PostJsonAsync($"repos/{repo}/releases",
            new { tag_name = tagName, name, body }, token, ct);
    }

    public async Task DispatchWorkflowAsync(Guid projectId, string workflowFileName, string gitRef,
        IReadOnlyDictionary<string, string>? inputs = null, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var _ = await PostJsonAsync(
            $"repos/{repo}/actions/workflows/{workflowFileName}/dispatches",
            new { @ref = gitRef, inputs = inputs ?? new Dictionary<string, string>() }, token, ct);
    }

    public async Task<IReadOnlyList<GitHubWorkflowRunDto>> GetWorkflowRunsAsync(Guid projectId, CancellationToken ct = default)
    {
        var (repo, token) = await GetRepoAsync(projectId, ct);
        using var doc = await GetJsonAsync($"repos/{repo}/actions/runs?per_page=30", token, ct);
        if (doc is null) return [];

        var list = new List<GitHubWorkflowRunDto>();
        foreach (var run in doc.RootElement.GetProperty("workflow_runs").EnumerateArray())
        {
            list.Add(new GitHubWorkflowRunDto(
                run.GetProperty("id").GetInt64(),
                run.GetProperty("name").GetString() ?? "",
                run.GetProperty("status").GetString() ?? "",
                run.TryGetProperty("conclusion", out var c) ? c.GetString() : null,
                run.GetProperty("html_url").GetString() ?? "",
                run.GetProperty("created_at").GetDateTime()));
        }
        return list;
    }

    private async Task<(string Repo, string Token)> GetRepoAsync(Guid projectId, CancellationToken ct)
    {
        var integration = await _resolver.ResolveAsync(projectId, IntegrationType.GitHub, ct)
            ?? throw new InvalidOperationException("La integración con GitHub no está configurada para el proyecto.");
        var repo = integration.Extra.GetValueOrDefault("repository")
            ?? throw new InvalidOperationException("La integración con GitHub no define 'repository' (owner/repo).");
        return (repo, integration.Token ?? string.Empty);
    }

    private async Task<JsonDocument?> GetJsonAsync(string path, string token, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, path, token);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub respondió {Status} para GET {Path}", response.StatusCode, path);
            return null;
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<JsonDocument?> PostJsonAsync(string path, object body, string token, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Post, path, token);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("GitHub respondió {Status} para POST {Path}: {Detail}",
                response.StatusCode, path, detail);
            throw new InvalidOperationException($"GitHub rechazó la operación ({response.StatusCode}).");
        }
        var content = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(content) ? null : JsonDocument.Parse(content);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, $"https://api.github.com/{path}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("QAGuardian", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
