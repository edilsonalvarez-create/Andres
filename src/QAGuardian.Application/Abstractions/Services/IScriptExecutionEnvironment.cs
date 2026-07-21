namespace QAGuardian.Application.Abstractions.Services;

/// <summary>
/// Opciones de sandbox de runners (Sprint 13). Sección de configuración: <c>Runners</c>.
/// </summary>
public sealed class RunnerSandboxOptions
{
    public const string SectionName = "Runners";

    /// <summary>
    /// Si es true, <see cref="ISandboxedProcessExecutor"/> usa el runtime de contenedor (13-B).
    /// Development suele ser false (fallback local). Compose de producción: true.
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>Timeout por defecto si el request no especifica uno.</summary>
    public int DefaultTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Prefijos de variables de entorno permitidas hacia el proceso/sandbox.
    /// Cualquier otra clave del request se descarta (anti-filtración de secretos de plataforma).
    /// </summary>
    public string[] EnvironmentAllowlistPrefixes { get; set; } =
    [
        "QA_GUARDIAN_",
        "PLAYWRIGHT_",
        "NODE_",
        "NPM_",
        "JAVA_",
        "JM_",
        "ZAP_",
        "PATH",
        "PATHEXT",
        "HOME",
        "USER",
        "LANG",
        "LC_",
        "TMP",
        "TEMP",
        "TMPDIR",
        "SYSTEMROOT",
        "WINDIR",
        "COMSPEC",
        "NUMBER_OF_PROCESSORS",
        "PROCESSOR_",
        "OS"
    ];

    /// <summary>Raíz de workspaces efímeros (relativa al content root o absoluta).</summary>
    public string WorkspaceRoot { get; set; } = "storage/runner-workspaces";

    /// <summary>Imagen Docker por defecto (Newman). Requiere build previo.</summary>
    public string SandboxImage { get; set; } = "qaguardian/runner-newman:local";

    /// <summary>Imagen Playwright / Visual / Selenium IDE (Node + browsers).</summary>
    public string SandboxImagePlaywright { get; set; } = "qaguardian/runner-playwright:local";

    /// <summary>Imagen JMeter non-root.</summary>
    public string SandboxImageJMeter { get; set; } = "qaguardian/runner-jmeter:local";

    /// <summary>Imagen oficial OWASP ZAP (baseline). Override por request vía ContainerImage.</summary>
    public string SandboxImageZap { get; set; } = "ghcr.io/zaproxy/zaproxy:stable";

    /// <summary>
    /// Red del contenedor: solo <c>none</c> (default, sin egress) o <c>bridge</c> (opt-in).
    /// <c>host</c> y cualquier otro valor se rechazan (Sprint 19-B / B8).
    /// Con <c>bridge</c>, use <see cref="SandboxNetworkName"/> para una red Docker dedicada
    /// (recomendación operativa de egress; no es un firewall de aplicación).
    /// </summary>
    public string SandboxNetworkMode { get; set; } = "none";

    /// <summary>
    /// Nombre opcional de red Docker cuando <see cref="SandboxNetworkMode"/> es <c>bridge</c>.
    /// Si está vacío se usa <c>bridge</c>. Operación: crear la red con reglas de egress
    /// (<c>docker network create …</c>) y apuntar aquí el nombre.
    /// </summary>
    public string? SandboxNetworkName { get; set; }

    /// <summary>Usuario no-root dentro del contenedor (uid:gid).</summary>
    public string SandboxUser { get; set; } = "1000:1000";

    /// <summary>Prefijo de nombre de contenedor (incluye TestRunId al ejecutar).</summary>
    public string SandboxContainerNamePrefix { get; set; } = "qaguardian-run-";

    /// <summary>Ruta del workspace dentro del contenedor.</summary>
    public string SandboxContainerWorkdir { get; set; } = "/workspace";

    /// <summary>Límite de memoria del contenedor (ej. 512m). Vacío = sin --memory.</summary>
    public string? SandboxMemoryLimit { get; set; } = "512m";

    /// <summary>Límite de CPUs del contenedor (ej. 1.0). Vacío = sin --cpus.</summary>
    public string? SandboxCpus { get; set; } = "1.0";

    /// <summary>Límite de procesos (PIDs) del contenedor. Null o ≤0 = sin --pids-limit.</summary>
    public int? SandboxPidsLimit { get; set; } = 256;

    /// <summary>CLI Docker en el host/API (<c>docker</c> en PATH).</summary>
    public string DockerCli { get; set; } = "docker";
}

/// <summary>Petición de ejecución de proceso bajo contrato de sandbox (Sprint 13).</summary>
/// <param name="ContainerImage">Imagen Docker opcional (si null, usa <see cref="RunnerSandboxOptions.SandboxImage"/>).</param>
/// <param name="PreferHostDockerCli">
/// True = orquestar con <c>docker</c> en el host (p. ej. ZAP ya corre en su contenedor oficial),
/// sin anidar otro sandbox. No ejecuta el script de usuario en el proceso API.
/// </param>
public sealed record ScriptExecutionRequest(
    Guid TestRunId,
    string FileName,
    string Arguments,
    string WorkingDirectory,
    TimeSpan? Timeout,
    IReadOnlyDictionary<string, string>? Environment,
    IReadOnlyList<string>? ArtifactGlobs,
    string? ContainerImage = null,
    bool PreferHostDockerCli = false);

/// <summary>Resultado de ejecución: códigos, streams y artefactos recolectados.</summary>
public sealed record ScriptExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    IReadOnlyList<string> ArtifactPaths);

/// <summary>
/// Workspace efímero por TestRun: directorio aislado que se elimina al disponer.
/// </summary>
public interface IScriptWorkspace : IAsyncDisposable
{
    Guid TestRunId { get; }
    string RootPath { get; }

    /// <summary>Escribe un archivo relativo al workspace.</summary>
    Task WriteAllTextAsync(string relativePath, string contents, CancellationToken ct = default);

    /// <summary>Lista rutas absolutas que coinciden con globs relativos (ej. **/*.png).</summary>
    IReadOnlyList<string> CollectArtifacts(IEnumerable<string> relativeGlobs);
}

/// <summary>
/// Crea workspaces efímeros para ejecución de scripts de usuario (Sprint 13 seams).
/// </summary>
public interface IScriptExecutionEnvironment
{
    Task<IScriptWorkspace> CreateWorkspaceAsync(Guid testRunId, CancellationToken ct = default);
}

/// <summary>
/// Ejecutor de procesos con contrato de sandbox: env allowlist, timeout, artifacts.
/// Implementaciones: local (Dev) y contenedor efímero (13-B).
/// </summary>
public interface ISandboxedProcessExecutor
{
    /// <summary>True si esta instancia usa aislamiento de contenedor (no solo Process en el host API).</summary>
    bool UsesContainerSandbox { get; }

    Task<ScriptExecutionResult> RunAsync(ScriptExecutionRequest request, CancellationToken ct = default);
}
