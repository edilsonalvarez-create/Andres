using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace QAGuardian.Infrastructure.Runners;

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

/// <summary>Ejecuta procesos externos (npx, jmeter, docker) con timeout y captura de salida.</summary>
public class ProcessExecutor
{
    private readonly ILogger<ProcessExecutor> _logger;

    public ProcessExecutor(ILogger<ProcessExecutor> logger) => _logger = logger;

    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(30);
        var psi = new ProcessStartInfo
        {
            FileName = ResolveExecutable(fileName),
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (environment is not null)
            foreach (var (key, value) in environment)
                psi.Environment[key] = value;

        _logger.LogInformation("Ejecutando: {FileName} {Arguments}", psi.FileName, arguments);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(effectiveTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* el proceso pudo terminar */ }
            _logger.LogWarning("Proceso {FileName} excedió el timeout de {Timeout}", fileName, effectiveTimeout);
            return new ProcessResult(-1, stdout.ToString(), stderr.ToString(), true);
        }
    }

    /// <summary>
    /// Resuelve el ejecutable a una ruta completa. En Windows, herramientas como npx/newman/jmeter
    /// son shims .cmd/.bat; con UseShellExecute=false, Process.Start no aplica PATHEXT, así que
    /// hay que localizar la extensión correcta a lo largo del PATH. En caso de no encontrarlo,
    /// se devuelve el nombre original para conservar el comportamiento y el mensaje de error nativo.
    /// </summary>
    private static string ResolveExecutable(string fileName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return fileName;

        // Ya trae ruta o extensión ejecutable: se usa tal cual.
        if (Path.IsPathRooted(fileName) || Path.HasExtension(fileName))
            return fileName;

        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in pathDirs)
        {
            foreach (var ext in pathExt)
            {
                var candidate = Path.Combine(dir, fileName + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return fileName;
    }
}
