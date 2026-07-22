using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Runner de Selenium IDE: ejecuta proyectos <c>.side</c> con
/// <c>npx selenium-side-runner</c> (formato Jest) vía <see cref="ISandboxedProcessExecutor"/>.
/// </summary>
public class SeleniumIdeTestRunner : ITestRunner
{
    private readonly ISandboxedProcessExecutor _executor;
    private readonly RunnerSandboxOptions _options;
    private readonly ILogger<SeleniumIdeTestRunner> _logger;

    public SeleniumIdeTestRunner(
        ISandboxedProcessExecutor executor,
        IOptions<RunnerSandboxOptions> options,
        ILogger<SeleniumIdeTestRunner> logger)
    {
        _executor = executor;
        _options = options.Value;
        _logger = logger;
    }

    public AutomationFramework Framework => AutomationFramework.SeleniumIde;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (context.Scripts.Count == 0)
            return new RunnerOutcome(true, [], [], null,
                "No hay proyectos .side de Selenium IDE para ejecutar.");

        var results = new List<RunnerResultItem>();
        var sandbox = _executor.UsesContainerSandbox;

        foreach (var script in context.Scripts)
        {
            var outputDirName = $"selenium-{Guid.NewGuid():N}";
            var outputDir = Path.Combine(context.WorkingDirectory, outputDirName);
            Directory.CreateDirectory(outputDir);

            var outArg = sandbox ? outputDirName : outputDir;
            var scriptArg = sandbox
                ? RunnerSandboxPaths.ToWorkspaceRelative(script.ScriptPath, context.WorkingDirectory)
                : script.ScriptPath;

            var run = await _executor.RunAsync(new ScriptExecutionRequest(
                context.TestRunId, "npx",
                $"selenium-side-runner --output-directory \"{outArg}\" --output-format jest \"{scriptArg}\"",
                context.WorkingDirectory, TimeSpan.FromMinutes(30), null,
                [$"{outputDirName}/**/*.json", "**/*.json"],
                sandbox ? _options.SandboxImagePlaywright : null), ct);

            var reportFile = Directory.Exists(outputDir)
                ? Directory.EnumerateFiles(outputDir, "*.json", SearchOption.AllDirectories).FirstOrDefault()
                : null;

            if (reportFile is not null)
            {
                var parsed = ParseJestReport(await File.ReadAllTextAsync(reportFile, ct), script, reportFile);
                if (parsed.Count > 0)
                {
                    results.AddRange(parsed);
                    continue;
                }
            }

            var passed = run.ExitCode == 0 && !run.TimedOut;
            var error = passed
                ? null
                : run.TimedOut
                    ? "La ejecución de Selenium IDE excedió el tiempo límite."
                    : Truncate($"selenium-side-runner terminó con código {run.ExitCode}. " +
                               $"{run.StandardError}{run.StandardOutput}");
            results.Add(new RunnerResultItem(script.TestCaseId, script.Name,
                passed ? ResultStatus.Passed : ResultStatus.Failed, 0, error, null, null, []));
        }

        return new RunnerOutcome(true, results, [], null, null);
    }

    private List<RunnerResultItem> ParseJestReport(string json, TestScriptRef script, string reportPath)
    {
        var results = new List<RunnerResultItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("testResults", out var testResults))
                return results;

            var evidence = new RunnerEvidence(EvidenceType.JsonReport, reportPath, "application/json",
                new FileInfo(reportPath).Length);

            foreach (var suite in testResults.EnumerateArray())
            {
                if (!suite.TryGetProperty("assertionResults", out var assertions)) continue;
                foreach (var a in assertions.EnumerateArray())
                {
                    var title = a.TryGetProperty("fullName", out var fn) && fn.GetString() is { Length: > 0 } full
                        ? full
                        : a.TryGetProperty("title", out var t) ? t.GetString() ?? script.Name : script.Name;

                    var status = a.TryGetProperty("status", out var st) ? st.GetString() switch
                    {
                        "passed" => ResultStatus.Passed,
                        "pending" or "skipped" or "todo" => ResultStatus.Skipped,
                        _ => ResultStatus.Failed
                    } : ResultStatus.Failed;

                    long duration = a.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                        ? d.GetInt64() : 0;

                    string? error = null;
                    if (status == ResultStatus.Failed && a.TryGetProperty("failureMessages", out var fm)
                        && fm.ValueKind == JsonValueKind.Array && fm.GetArrayLength() > 0)
                        error = Truncate(string.Join("\n", fm.EnumerateArray().Select(m => m.GetString())));

                    results.Add(new RunnerResultItem(script.TestCaseId, $"{script.Name} / {title}",
                        status, duration, error, null, null, [evidence]));
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "No se pudo parsear el reporte Jest de Selenium IDE en {Path}", reportPath);
        }
        return results;
    }

    private static string Truncate(string value, int max = 2000)
        => value.Length <= max ? value : value[..max] + "…";
}
