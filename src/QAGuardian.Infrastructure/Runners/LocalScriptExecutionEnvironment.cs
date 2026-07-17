using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>Crea directorios efímeros bajo <see cref="RunnerSandboxOptions.WorkspaceRoot"/>.</summary>
public sealed class LocalScriptExecutionEnvironment : IScriptExecutionEnvironment
{
    private readonly RunnerSandboxOptions _options;
    private readonly IHostEnvironment _host;
    private readonly ILogger<LocalScriptExecutionEnvironment> _logger;

    public LocalScriptExecutionEnvironment(
        IOptions<RunnerSandboxOptions> options,
        IHostEnvironment host,
        ILogger<LocalScriptExecutionEnvironment> logger)
    {
        _options = options.Value;
        _host = host;
        _logger = logger;
    }

    public Task<IScriptWorkspace> CreateWorkspaceAsync(Guid testRunId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var root = Path.IsPathRooted(_options.WorkspaceRoot)
            ? _options.WorkspaceRoot
            : Path.Combine(_host.ContentRootPath, _options.WorkspaceRoot);

        var path = Path.Combine(root, testRunId.ToString("N"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _logger.LogDebug("Workspace efímero creado: {Path}", path);
        return Task.FromResult<IScriptWorkspace>(new LocalScriptWorkspace(testRunId, path, _logger));
    }
}

internal sealed class LocalScriptWorkspace : IScriptWorkspace
{
    private readonly ILogger _logger;
    private bool _disposed;

    public LocalScriptWorkspace(Guid testRunId, string rootPath, ILogger logger)
    {
        TestRunId = testRunId;
        RootPath = rootPath;
        _logger = logger;
    }

    public Guid TestRunId { get; }
    public string RootPath { get; }

    public async Task WriteAllTextAsync(string relativePath, string contents, CancellationToken ct = default)
    {
        var full = ResolveSafe(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, contents, ct);
    }

    public IReadOnlyList<string> CollectArtifacts(IEnumerable<string> relativeGlobs)
        => WorkspaceArtifactCollector.Collect(RootPath, relativeGlobs);

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _disposed = true;
        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar workspace {Path}", RootPath);
        }

        return ValueTask.CompletedTask;
    }

    private string ResolveSafe(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        var rootFull = Path.GetFullPath(RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFullPath(RootPath), combined, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de workspace fuera del directorio efímero.");
        return combined;
    }

}

/// <summary>Recolección mínima de artefactos por glob simple (sin dependencia extra).</summary>
internal static class WorkspaceArtifactCollector
{
    public static IReadOnlyList<string> Collect(string rootPath, IEnumerable<string> relativeGlobs)
    {
        var rootFull = Path.GetFullPath(rootPath);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var glob in relativeGlobs)
        {
            if (string.IsNullOrWhiteSpace(glob))
                continue;

            var pattern = glob.Replace('\\', '/').Trim();
            if (pattern.StartsWith("**/", StringComparison.Ordinal))
            {
                var suffix = pattern[3..];
                foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
                    if (MatchesSimpleGlob(rel, suffix) || MatchesSimpleGlob(Path.GetFileName(file), suffix))
                        found.Add(file);
                }
            }
            else
            {
                var candidate = Path.GetFullPath(Path.Combine(rootFull, pattern));
                if (!candidate.StartsWith(
                        rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(candidate, rootFull, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(candidate))
                    found.Add(candidate);
                else
                {
                    var dir = Path.GetDirectoryName(candidate) ?? rootFull;
                    var name = Path.GetFileName(candidate);
                    if (Directory.Exists(dir))
                        foreach (var file in Directory.EnumerateFiles(dir, name, SearchOption.TopDirectoryOnly))
                            found.Add(file);
                }
            }
        }

        return found.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool MatchesSimpleGlob(string value, string pattern)
    {
        if (!pattern.Contains('*'))
            return value.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith('/' + pattern, StringComparison.OrdinalIgnoreCase);

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return value.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);

        return false;
    }
}
