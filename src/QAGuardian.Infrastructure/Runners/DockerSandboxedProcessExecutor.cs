using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Ejecuta scripts de usuario en un contenedor Docker efímero (<c>docker run --rm</c>).
/// No monta el filesystem del API ni propaga secretos de plataforma (Sprint 13-B).
/// </summary>
public sealed class DockerSandboxedProcessExecutor : ISandboxedProcessExecutor
{
    private readonly ProcessExecutor _dockerCli;
    private readonly LocalSandboxedProcessExecutor _envFilter;
    private readonly RunnerSandboxOptions _options;
    private readonly ILogger<DockerSandboxedProcessExecutor> _logger;

    public DockerSandboxedProcessExecutor(
        ProcessExecutor dockerCli,
        LocalSandboxedProcessExecutor envFilter,
        IOptions<RunnerSandboxOptions> options,
        ILogger<DockerSandboxedProcessExecutor> logger)
    {
        _dockerCli = dockerCli;
        _envFilter = envFilter;
        _options = options.Value;
        _logger = logger;
    }

    public bool UsesContainerSandbox => true;

    public async Task<ScriptExecutionResult> RunAsync(
        ScriptExecutionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkingDirectory)
            || !Directory.Exists(request.WorkingDirectory))
            throw new DirectoryNotFoundException(
                $"WorkingDirectory inexistente: {request.WorkingDirectory}");

        // PreferHostDockerCli: escape hatch legado (ZAP ya no lo usa; Sprint 19-A).
        // Orquesta `docker` en el host sin anidar otro sandbox ni heredar secretos del API.
        if (request.PreferHostDockerCli)
            return await RunHostDockerCliAsync(request, ct);

        await CleanupOrphansAsync(request.TestRunId, ct);

        var timeout = request.Timeout
            ?? TimeSpan.FromMinutes(Math.Max(1, _options.DefaultTimeoutMinutes));
        var env = _envFilter.FilterEnvironment(request.Environment);
        var containerName = BuildContainerName(request.TestRunId);
        var workdirHost = Path.GetFullPath(request.WorkingDirectory);
        var workdirContainer = _options.SandboxContainerWorkdir.TrimEnd('/');
        var image = string.IsNullOrWhiteSpace(request.ContainerImage)
            ? _options.SandboxImage
            : request.ContainerImage!;
        var fileName = request.FileName;
        var arguments = RewritePathsForContainer(request.Arguments, workdirHost, workdirContainer);

        var dockerArgs = BuildDockerRunArguments(
            containerName, workdirHost, workdirContainer, env, fileName, arguments, image);

        _logger.LogInformation(
            "Sandbox Docker TestRun={TestRunId} container={Name} image={Image} network={Network}",
            request.TestRunId, containerName, image, _options.SandboxNetworkMode);

        ProcessResult result;
        try
        {
            result = await _dockerCli.RunAsync(
                _options.DockerCli,
                dockerArgs,
                workingDirectory: null,
                timeout: timeout,
                environment: null, // NUNCA heredar env del proceso API
                ct);
        }
        catch (Exception)
        {
            await ForceRemoveAsync(containerName, CancellationToken.None);
            throw;
        }

        if (result.TimedOut)
            await ForceRemoveAsync(containerName, CancellationToken.None);

        var artifacts = request.ArtifactGlobs is { Count: > 0 }
            ? WorkspaceArtifactCollector.Collect(workdirHost, request.ArtifactGlobs)
            : Array.Empty<string>();

        return new ScriptExecutionResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            result.TimedOut,
            artifacts);
    }

    private async Task<ScriptExecutionResult> RunHostDockerCliAsync(
        ScriptExecutionRequest request, CancellationToken ct)
    {
        var timeout = request.Timeout
            ?? TimeSpan.FromMinutes(Math.Max(1, _options.DefaultTimeoutMinutes));
        var env = _envFilter.FilterEnvironment(request.Environment);

        _logger.LogInformation(
            "Orquestación docker CLI (host) TestRun={TestRunId}: {File} (sin secretos de plataforma)",
            request.TestRunId, request.FileName);

        var result = await _dockerCli.RunAsync(
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
            result.ExitCode, result.StandardOutput, result.StandardError, result.TimedOut, artifacts);
    }

    /// <summary>Construye argumentos de <c>docker run</c> (expuesto para tests de aislamiento).</summary>
    public string BuildDockerRunArguments(
        string containerName,
        string hostWorkdir,
        string containerWorkdir,
        IReadOnlyDictionary<string, string> environment,
        string fileName,
        string arguments,
        string? image = null)
    {
        image ??= _options.SandboxImage;
        var network = ResolveNetwork(_options.SandboxNetworkMode, _options.SandboxNetworkName);
        var sb = new StringBuilder();
        sb.Append("run --rm ");
        sb.Append("--name ").Append(Quote(containerName)).Append(' ');
        sb.Append("--user ").Append(Quote(_options.SandboxUser)).Append(' ');
        sb.Append("--network ").Append(Quote(network)).Append(' ');
        sb.Append("--workdir ").Append(Quote(containerWorkdir)).Append(' ');
        // Solo el workspace del TestRun — nunca /app del API ni .env
        sb.Append("-v ").Append(Quote($"{ToDockerPath(hostWorkdir)}:{containerWorkdir}")).Append(' ');
        // Read-only root FS salvo /workspace (tmpfs para /tmp)
        sb.Append("--read-only ");
        sb.Append("--tmpfs /tmp:rw,noexec,nosuid,size=64m ");
        sb.Append("--cap-drop ALL ");
        sb.Append("--security-opt no-new-privileges ");

        if (!string.IsNullOrWhiteSpace(_options.SandboxMemoryLimit))
            sb.Append("--memory ").Append(Quote(_options.SandboxMemoryLimit)).Append(' ');

        if (!string.IsNullOrWhiteSpace(_options.SandboxCpus))
            sb.Append("--cpus ").Append(Quote(_options.SandboxCpus)).Append(' ');

        if (_options.SandboxPidsLimit is > 0)
            sb.Append("--pids-limit ").Append(_options.SandboxPidsLimit.Value).Append(' ');

        foreach (var (key, value) in environment)
            sb.Append("-e ").Append(Quote($"{key}={value}")).Append(' ');

        sb.Append(Quote(image)).Append(' ');
        sb.Append(Quote(fileName));
        if (!string.IsNullOrWhiteSpace(arguments))
            sb.Append(' ').Append(arguments);

        return sb.ToString();
    }

    public string BuildContainerName(Guid testRunId)
    {
        var name = $"{_options.SandboxContainerNamePrefix}{testRunId:N}-{Guid.NewGuid():N}";
        return name.Length <= 63 ? name : name[..63];
    }

    public static string RewritePathsForContainer(
        string arguments, string hostWorkdir, string containerWorkdir)
    {
        if (string.IsNullOrEmpty(arguments))
            return arguments;

        var full = Path.GetFullPath(hostWorkdir);
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            full,
            full.Replace('\\', '/'),
            ToDockerPath(full)
        };

        var result = arguments;
        foreach (var hostPath in variants.OrderByDescending(v => v.Length))
        {
            if (hostPath.Length == 0)
                continue;
            result = Regex.Replace(
                result,
                Regex.Escape(hostPath),
                containerWorkdir,
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    internal static string ToDockerPath(string hostPath)
    {
        var full = Path.GetFullPath(hostPath).Replace('\\', '/');
        // Docker Desktop Windows: C:\foo → /c/foo o //c/foo; el engine acepta rutas Windows en -v.
        return full;
    }

    /// <summary>
    /// Modos permitidos: <c>none</c> (default) o <c>bridge</c>.
    /// <c>host</c> y cualquier otro valor → <see cref="InvalidOperationException"/> (Sprint 19-B / B8).
    /// </summary>
    internal static string NormalizeNetwork(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return "none";
        var m = mode.Trim().ToLowerInvariant();
        if (m is "none" or "bridge")
            return m;
        throw new InvalidOperationException(
            $"Runners:SandboxNetworkMode '{mode}' no permitido. " +
            "Valores admitidos: none | bridge. El modo 'host' está vetado (Sprint 19-B / B8).");
    }

    /// <summary>
    /// Resuelve el valor de <c>--network</c>: con mode <c>bridge</c> y
    /// <paramref name="networkName"/> configurado, usa esa red Docker dedicada (gancho de egress).
    /// </summary>
    internal static string ResolveNetwork(string? mode, string? networkName)
    {
        var normalized = NormalizeNetwork(mode);
        if (normalized == "bridge"
            && !string.IsNullOrWhiteSpace(networkName))
            return networkName.Trim();
        return normalized;
    }

    private static string Quote(string value)
    {
        if (value.Contains('"', StringComparison.Ordinal))
            value = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{value}\"";
    }

    private async Task CleanupOrphansAsync(Guid testRunId, CancellationToken ct)
    {
        var filter = $"{_options.SandboxContainerNamePrefix}{testRunId:N}";
        var list = await _dockerCli.RunAsync(
            _options.DockerCli,
            $"ps -aq --filter name={filter}",
            timeout: TimeSpan.FromSeconds(30),
            ct: ct);

        var ids = list.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var id in ids)
        {
            _logger.LogWarning("Eliminando contenedor sandbox huérfano {Id} (TestRun {RunId})", id, testRunId);
            await ForceRemoveAsync(id, ct);
        }
    }

    private async Task ForceRemoveAsync(string nameOrId, CancellationToken ct)
    {
        try
        {
            await _dockerCli.RunAsync(
                _options.DockerCli,
                $"rm -f {Quote(nameOrId)}",
                timeout: TimeSpan.FromSeconds(60),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cleanup docker rm -f {Name} (puede no existir)", nameOrId);
        }
    }
}
