namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Métricas de calidad de código obtenidas de SonarQube.</summary>
public record SonarMetricsDto(
    string ProjectKey,
    decimal CoveragePercent,
    decimal DuplicationPercent,
    int Bugs,
    int Vulnerabilities,
    int SecurityHotspots,
    int CodeSmells,
    string QualityGateStatus);

/// <summary>Puerto: cliente de la API de SonarQube.</summary>
public interface ISonarQubeClient
{
    Task<SonarMetricsDto?> GetMetricsAsync(Guid projectId, string projectKey, CancellationToken ct = default);
}

public record GitHubPullRequestDto(
    int Number, string Title, string State, string Author,
    string HeadBranch, string BaseBranch, string HeadSha, string Url,
    DateTime CreatedAt, bool Mergeable);

public record GitHubCheckRunDto(long Id, string Name, string Status, string? Conclusion, string Url);

public record GitHubWorkflowRunDto(long Id, string Name, string Status, string? Conclusion, string Url, DateTime CreatedAt);

/// <summary>Puerto: cliente de la API REST de GitHub.</summary>
public interface IGitHubClient
{
    Task<IReadOnlyList<GitHubPullRequestDto>> GetPullRequestsAsync(Guid projectId, string state = "open", CancellationToken ct = default);
    Task<GitHubCheckRunDto> CreateCheckRunAsync(Guid projectId, string headSha, string name,
        string status, string? conclusion, string title, string summary, CancellationToken ct = default);
    Task CommentOnPullRequestAsync(Guid projectId, int prNumber, string comment, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPullRequestChangedFilesAsync(Guid projectId, int prNumber, CancellationToken ct = default);
    Task CreateReleaseAsync(Guid projectId, string tagName, string name, string body, CancellationToken ct = default);
    Task DispatchWorkflowAsync(Guid projectId, string workflowFileName, string gitRef,
        IReadOnlyDictionary<string, string>? inputs = null, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubWorkflowRunDto>> GetWorkflowRunsAsync(Guid projectId, CancellationToken ct = default);
}

/// <summary>Diferencia detectada entre esquemas de base de datos.</summary>
public record SchemaDifference(string Category, string ObjectName, string Description);

/// <summary>Puerto: validador de esquemas SQL Server entre ambientes.</summary>
public interface IDatabaseSchemaValidator
{
    Task<IReadOnlyList<SchemaDifference>> CompareAsync(
        string sourceConnectionString, string targetConnectionString, CancellationToken ct = default);
}
