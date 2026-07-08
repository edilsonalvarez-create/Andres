using System.Diagnostics;
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
            FileName = fileName,
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

        _logger.LogInformation("Ejecutando: {FileName} {Arguments}", fileName, arguments);

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
}
