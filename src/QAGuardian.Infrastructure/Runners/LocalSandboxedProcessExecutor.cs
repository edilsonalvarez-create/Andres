using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Fallback Dev: ejecuta en el host API vía <see cref="ProcessExecutor"/> aplicando
/// allowlist de entorno y recolección de artefactos. No aísla en contenedor.
/// </summary>
public sealed class LocalSandboxedProcessExecutor : ISandboxedProcessExecutor
{
    private readonly ProcessExecutor _process;
    private readonly RunnerSandboxOptions _options;
    private readonly ILogger<LocalSandboxedProcessExecutor> _logger;
    private int _warned;

    public LocalSandboxedProcessExecutor(
        ProcessExecutor process,
        IOptions<RunnerSandboxOptions> options,
        ILogger<LocalSandboxedProcessExecutor> logger)
    {
        _process = process;
        _options = options.Value;
        _logger = logger;
    }

    public bool UsesContainerSandbox => false;

    public async Task<ScriptExecutionResult> RunAsync(
        ScriptExecutionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("FileName es obligatorio.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkingDirectory)
            || !Directory.Exists(request.WorkingDirectory))
            throw new DirectoryNotFoundException(
                $"WorkingDirectory inexistente: {request.WorkingDirectory}");

        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            _logger.LogWarning(
                "Runners: fallback LOCAL activo (solo Development). Los scripts de usuario se ejecutan " +
                "en el host API sin contenedor. Active Runners:UseSandbox=true + Docker para aislamiento.");
        }

        var timeout = request.Timeout
            ?? TimeSpan.FromMinutes(Math.Max(1, _options.DefaultTimeoutMinutes));
        var env = FilterEnvironment(request.Environment);

        _logger.LogInformation(
            "Ejecución local (sandbox off) TestRun={TestRunId}: {File} {Args}",
            request.TestRunId, request.FileName, request.Arguments);

        var result = await _process.RunAsync(
            request.FileName,
            request.Arguments,
            request.WorkingDirectory,
            timeout,
            env,
            ct);

        var artifacts = request.ArtifactGlobs is { Count: > 0 }
            ? WorkspaceArtifactCollector.Collect(request.WorkingDirectory, request.ArtifactGlobs)
            : Array.Empty<string>();

        return new ScriptExecutionResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            result.TimedOut,
            artifacts);
    }

    /// <summary>Aplica allowlist + denylist de secretos (expuesto para tests de seams).</summary>
    public IReadOnlyDictionary<string, string> FilterEnvironment(
        IReadOnlyDictionary<string, string>? requested)
    {
        var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (requested is null)
            return filtered;

        var prefixes = _options.EnvironmentAllowlistPrefixes ?? [];
        foreach (var (key, value) in requested)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (IsDeniedSecretKey(key))
            {
                _logger.LogWarning("Variable de entorno bloqueada (secreto de plataforma): {Key}", key);
                continue;
            }

            if (prefixes.Any(p => key.Equals(p, StringComparison.OrdinalIgnoreCase)
                                  || key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                filtered[key] = value;
            else
                _logger.LogDebug("Variable de entorno omitida (fuera de allowlist): {Key}", key);
        }

        return filtered;
    }

    private static bool IsDeniedSecretKey(string key)
    {
        ReadOnlySpan<string> denied =
        [
            "ConnectionStrings", "JWT", "SigningKey", "EncryptionKey", "ADMIN_PASSWORD",
            "Seed__AdminPassword", "SQL_SA", "PASSWORD", "ApiKey", "ANTHROPIC", "Redis"
        ];
        foreach (var d in denied)
        {
            if (key.Contains(d, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
