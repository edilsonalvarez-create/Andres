using System.Text.Json;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Runner de Playwright: ejecuta specs vía `npx playwright test` con reporter JSON
/// y recolecta screenshots/videos como evidencia.
/// </summary>
public class PlaywrightTestRunner : ITestRunner
{
    private readonly ISandboxedProcessExecutor _executor;
    private readonly RunnerSandboxOptions _options;
    private readonly ILogger<PlaywrightTestRunner> _logger;

    public PlaywrightTestRunner(
        ISandboxedProcessExecutor executor,
        Microsoft.Extensions.Options.IOptions<RunnerSandboxOptions> options,
        ILogger<PlaywrightTestRunner> logger)
    {
        _executor = executor;
        _options = options.Value;
        _logger = logger;
    }

    public AutomationFramework Framework => AutomationFramework.Playwright;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (context.Scripts.Count == 0)
            return new RunnerOutcome(true, [], [], null,
                "No hay casos de prueba automatizados con Playwright para este proyecto.");

        var reportFile = "playwright-report.json";
        var reportPath = Path.Combine(context.WorkingDirectory, reportFile);
        var sandbox = _executor.UsesContainerSandbox;
        var specs = RunnerSandboxPaths.QuoteJoin(
            context.Scripts.Select(s => s.ScriptPath), context.WorkingDirectory, sandbox);
        var reportArg = sandbox ? reportFile : reportPath;
        var env = new Dictionary<string, string>
        {
            ["PLAYWRIGHT_JSON_OUTPUT_NAME"] = reportArg,
            ["QA_GUARDIAN_ENV"] = context.Environment.ToString()
        };

        var result = await _executor.RunAsync(new ScriptExecutionRequest(
            context.TestRunId, "npx",
            $"playwright test {specs} --reporter=json",
            context.WorkingDirectory, TimeSpan.FromMinutes(60), env,
            ["**/*.png", "**/*.webm", reportFile],
            sandbox ? _options.SandboxImagePlaywright : null), ct);

        if (!File.Exists(reportPath))
        {
            // Sin reporte JSON no hay resultados individuales: la ejecución completa falló.
            return new RunnerOutcome(false, [], [], null,
                $"Playwright no generó reporte. Salida: {Truncate(result.StandardError + result.StandardOutput)}");
        }

        var results = ParsePlaywrightReport(await File.ReadAllTextAsync(reportPath, ct), context);
        return new RunnerOutcome(true, results, [], null, null);
    }

    private List<RunnerResultItem> ParsePlaywrightReport(string json, TestRunContext context)
    {
        var results = new List<RunnerResultItem>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("suites", out var suites)) return results;

        void WalkSuite(JsonElement suite, string? scriptFile)
        {
            var file = suite.TryGetProperty("file", out var f) ? f.GetString() : scriptFile;
            if (suite.TryGetProperty("specs", out var specs))
            {
                foreach (var spec in specs.EnumerateArray())
                {
                    var title = spec.GetProperty("title").GetString() ?? "spec";
                    foreach (var test in spec.GetProperty("tests").EnumerateArray())
                    foreach (var attempt in test.GetProperty("results").EnumerateArray())
                    {
                        var status = attempt.GetProperty("status").GetString() switch
                        {
                            "passed" => ResultStatus.Passed,
                            "skipped" => ResultStatus.Skipped,
                            "flaky" => ResultStatus.Flaky,
                            _ => ResultStatus.Failed
                        };
                        var duration = attempt.TryGetProperty("duration", out var d) ? d.GetInt64() : 0;
                        string? error = null, stack = null;
                        if (attempt.TryGetProperty("error", out var err))
                        {
                            error = err.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                            stack = err.TryGetProperty("stack", out var st) ? st.GetString() : null;
                        }

                        var evidences = new List<RunnerEvidence>();
                        if (attempt.TryGetProperty("attachments", out var attachments))
                        {
                            foreach (var att in attachments.EnumerateArray())
                            {
                                var path = att.TryGetProperty("path", out var p) ? p.GetString() : null;
                                var contentType = att.TryGetProperty("contentType", out var c)
                                    ? c.GetString() ?? "application/octet-stream"
                                    : "application/octet-stream";
                                if (path is null || !File.Exists(path)) continue;
                                var type = contentType.StartsWith("image") ? EvidenceType.Screenshot
                                    : contentType.StartsWith("video") ? EvidenceType.Video
                                    : EvidenceType.Log;
                                evidences.Add(new RunnerEvidence(type, path, contentType, new FileInfo(path).Length));
                            }
                        }

                        var testCaseId = context.Scripts
                            .FirstOrDefault(s => file is not null && s.ScriptPath.EndsWith(file, StringComparison.OrdinalIgnoreCase))
                            ?.TestCaseId;

                        results.Add(new RunnerResultItem(testCaseId, title, status, duration, error, stack, null, evidences));
                    }
                }
            }
            if (suite.TryGetProperty("suites", out var children))
                foreach (var child in children.EnumerateArray())
                    WalkSuite(child, file);
        }

        foreach (var suite in suites.EnumerateArray())
            WalkSuite(suite, null);
        return results;
    }

    private static string Truncate(string value, int max = 2000)
        => value.Length <= max ? value : value[..max] + "…";
}

/// <summary>Runner de Postman: ejecuta collections con Newman y exporta JSON.</summary>
public class NewmanTestRunner : ITestRunner
{
    private readonly ISandboxedProcessExecutor _executor;
    private readonly RunnerSandboxOptions _options;

    public NewmanTestRunner(
        ISandboxedProcessExecutor executor,
        Microsoft.Extensions.Options.IOptions<RunnerSandboxOptions> options)
    {
        _executor = executor;
        _options = options.Value;
    }

    public AutomationFramework Framework => AutomationFramework.Postman;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (context.Scripts.Count == 0)
            return new RunnerOutcome(true, [], [], null,
                "No hay collections de Postman configuradas para este proyecto.");

        var results = new List<RunnerResultItem>();
        foreach (var script in context.Scripts)
        {
            var exportFileName = $"newman-{Guid.NewGuid():N}.json";
            var exportPath = Path.Combine(context.WorkingDirectory, exportFileName);
            var sandbox = _executor.UsesContainerSandbox;
            var scriptArg = sandbox
                ? RunnerSandboxPaths.ToWorkspaceRelative(script.ScriptPath, context.WorkingDirectory)
                : script.ScriptPath;
            var exportArg = sandbox ? exportFileName : exportPath;

            var envArg = context.Parameters.TryGetValue("postmanEnvironment", out var envFile)
                ? $" -e \"{(sandbox ? RunnerSandboxPaths.ToWorkspaceRelative(envFile, context.WorkingDirectory) : envFile)}\""
                : string.Empty;

            string fileName;
            string arguments;
            if (sandbox)
            {
                fileName = "newman";
                arguments =
                    $"run \"{scriptArg}\"{envArg} -r json --reporter-json-export \"{exportArg}\"";
            }
            else
            {
                fileName = "npx";
                arguments =
                    $"newman run \"{script.ScriptPath}\"{envArg} -r json --reporter-json-export \"{exportPath}\"";
            }

            var run = await _executor.RunAsync(new ScriptExecutionRequest(
                context.TestRunId,
                fileName,
                arguments,
                context.WorkingDirectory,
                TimeSpan.FromMinutes(30),
                new Dictionary<string, string>
                {
                    ["QA_GUARDIAN_ENV"] = context.Environment.ToString()
                },
                [exportFileName, "newman-*.json"],
                sandbox ? _options.SandboxImage : null), ct);

            if (!File.Exists(exportPath))
            {
                results.Add(new RunnerResultItem(script.TestCaseId, script.Name, ResultStatus.Failed, 0,
                    $"Newman no generó reporte (exit {run.ExitCode}).", run.StandardError, null, []));
                continue;
            }

            results.AddRange(ParseNewmanReport(await File.ReadAllTextAsync(exportPath, ct), script, exportPath));
        }
        return new RunnerOutcome(true, results, [], null, null);
    }

    private static List<RunnerResultItem> ParseNewmanReport(string json, TestScriptRef script, string reportPath)
    {
        var results = new List<RunnerResultItem>();
        using var doc = JsonDocument.Parse(json);
        var run = doc.RootElement.GetProperty("run");
        var evidence = new RunnerEvidence(EvidenceType.JsonReport, reportPath, "application/json",
            new FileInfo(reportPath).Length);

        if (run.TryGetProperty("executions", out var executions))
        {
            foreach (var execution in executions.EnumerateArray())
            {
                var itemName = execution.GetProperty("item").GetProperty("name").GetString() ?? "request";
                long duration = 0;
                string? metrics = null;
                if (execution.TryGetProperty("response", out var response))
                {
                    duration = response.TryGetProperty("responseTime", out var rt) ? rt.GetInt64() : 0;
                    var code = response.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : 0;
                    metrics = JsonSerializer.Serialize(new { statusCode = code, responseTimeMs = duration });
                }

                var failures = new List<string>();
                if (execution.TryGetProperty("assertions", out var assertions))
                {
                    foreach (var assertion in assertions.EnumerateArray())
                    {
                        if (assertion.TryGetProperty("error", out var err))
                            failures.Add($"{assertion.GetProperty("assertion").GetString()}: " +
                                         $"{(err.TryGetProperty("message", out var m) ? m.GetString() : "assert falló")}");
                    }
                }

                results.Add(new RunnerResultItem(
                    script.TestCaseId,
                    $"{script.Name} / {itemName}",
                    failures.Count == 0 ? ResultStatus.Passed : ResultStatus.Failed,
                    duration,
                    failures.Count == 0 ? null : string.Join("; ", failures),
                    null, metrics, [evidence]));
            }
        }
        return results;
    }
}

/// <summary>Runner de JMeter: ejecuta planes .jmx en modo no-GUI y agrega métricas de rendimiento.</summary>
public class JMeterTestRunner : ITestRunner
{
    private readonly ISandboxedProcessExecutor _executor;
    private readonly RunnerSandboxOptions _options;

    public JMeterTestRunner(
        ISandboxedProcessExecutor executor,
        Microsoft.Extensions.Options.IOptions<RunnerSandboxOptions> options)
    {
        _executor = executor;
        _options = options.Value;
    }

    public AutomationFramework Framework => AutomationFramework.JMeter;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (context.Scripts.Count == 0)
            return new RunnerOutcome(true, [], [], null,
                "No hay planes de JMeter configurados para este proyecto.");

        var results = new List<RunnerResultItem>();
        var aggregate = new Dictionary<string, object>();

        foreach (var script in context.Scripts)
        {
            var jtlFile = $"jmeter-{Guid.NewGuid():N}.jtl";
            var jtlPath = Path.Combine(context.WorkingDirectory, jtlFile);
            var sandbox = _executor.UsesContainerSandbox;
            var planArg = sandbox
                ? RunnerSandboxPaths.ToWorkspaceRelative(script.ScriptPath, context.WorkingDirectory)
                : script.ScriptPath;
            var jtlArg = sandbox ? jtlFile : jtlPath;

            var run = await _executor.RunAsync(new ScriptExecutionRequest(
                context.TestRunId, "jmeter",
                $"-n -t \"{planArg}\" -l \"{jtlArg}\" -Jjmeter.save.saveservice.output_format=csv",
                context.WorkingDirectory, TimeSpan.FromMinutes(90), null, [jtlFile],
                sandbox ? _options.SandboxImageJMeter : null), ct);

            if (!File.Exists(jtlPath))
            {
                results.Add(new RunnerResultItem(script.TestCaseId, script.Name, ResultStatus.Failed, 0,
                    $"JMeter no generó resultados (exit {run.ExitCode}).", run.StandardError, null, []));
                continue;
            }

            var (item, metrics) = ParseJtl(jtlPath, script);
            results.Add(item);
            aggregate[script.Name] = metrics;
        }

        // Uso de CPU/memoria del servidor bajo prueba: requiere un PerfMon Metrics
        // Collector en el plan .jmx que lea de un ServerAgent en la máquina objetivo.
        // La plataforma parsea y reporta esas métricas si el archivo está presente.
        var perfmonPath = context.Parameters.TryGetValue("perfmonResults", out var pm)
            ? pm : Path.Combine(context.WorkingDirectory, "perfmon.jtl");
        if (File.Exists(perfmonPath))
        {
            var resources = PerfMonParser.Parse(await File.ReadAllLinesAsync(perfmonPath, ct));
            if (resources.HasData)
                aggregate["resources"] = new
                {
                    cpuAvgPercent = resources.CpuAvgPercent,
                    cpuMaxPercent = resources.CpuMaxPercent,
                    memoryAvgMb = resources.MemoryAvgMb,
                    memoryMaxMb = resources.MemoryMaxMb
                };
        }

        return new RunnerOutcome(true, results, [], JsonSerializer.Serialize(aggregate), null);
    }

    private static (RunnerResultItem, object) ParseJtl(string jtlPath, TestScriptRef script)
    {
        var lines = File.ReadAllLines(jtlPath);
        var samples = ParseSamples(lines);

        if (samples.Count == 0)
        {
            return (new RunnerResultItem(script.TestCaseId, script.Name, ResultStatus.Failed, 0,
                "El plan no produjo muestras.", null, null, []), new { });
        }

        var durationSec = Math.Max(1, (samples.Max(s => s.Timestamp) - samples.Min(s => s.Timestamp)).TotalSeconds);
        var errors = samples.Count(s => !s.Success);
        var metrics = new
        {
            totalSamples = samples.Count,
            tps = Math.Round(samples.Count / durationSec, 2),
            avgResponseMs = Math.Round(samples.Average(s => s.Elapsed), 2),
            maxResponseMs = samples.Max(s => s.Elapsed),
            minResponseMs = samples.Min(s => s.Elapsed),
            // Usuarios concurrentes = máximo de hilos activos (columna allThreads del JTL).
            concurrentUsers = samples.Max(s => s.AllThreads),
            errorCount = errors,
            errorRatePercent = Math.Round(errors * 100.0 / samples.Count, 2)
        };

        var evidence = new RunnerEvidence(EvidenceType.Log, jtlPath, "text/csv", new FileInfo(jtlPath).Length);
        var status = metrics.errorRatePercent < 1 ? ResultStatus.Passed : ResultStatus.Failed;
        return (new RunnerResultItem(script.TestCaseId, script.Name, status,
            (long)metrics.avgResponseMs,
            status == ResultStatus.Failed ? $"Tasa de error {metrics.errorRatePercent}% (umbral 1%)" : null,
            null, JsonSerializer.Serialize(metrics), [evidence]), metrics);
    }

    public readonly record struct JtlSample(long Elapsed, bool Success, DateTimeOffset Timestamp, int AllThreads);

    /// <summary>
    /// Parsea las líneas de un JTL (CSV). Mapea las columnas por nombre a partir del
    /// encabezado (el orden del JTL es configurable); si no hay encabezado, usa el orden
    /// por defecto de JMeter (timeStamp, elapsed, ..., success en la posición 7).
    /// </summary>
    public static List<JtlSample> ParseSamples(IReadOnlyList<string> lines)
    {
        var samples = new List<JtlSample>();
        if (lines.Count == 0) return samples;

        var header = lines[0].Split(',');
        var hasHeader = header.Any(h => h.Equals("timeStamp", StringComparison.OrdinalIgnoreCase));

        int iTs = 0, iElapsed = 1, iSuccess = 7, iThreads = -1;
        if (hasHeader)
        {
            int Idx(string name) => Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            iTs = Idx("timeStamp");
            iElapsed = Idx("elapsed");
            iSuccess = Idx("success");
            iThreads = Idx("allThreads");
        }

        foreach (var line in lines.Skip(hasHeader ? 1 : 0))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            var maxNeeded = Math.Max(iTs, iElapsed);
            if (iTs < 0 || iElapsed < 0 || parts.Length <= maxNeeded) continue;
            if (!long.TryParse(parts[iTs], out var ts) || !long.TryParse(parts[iElapsed], out var elapsed)) continue;

            var success = iSuccess >= 0 && iSuccess < parts.Length
                && string.Equals(parts[iSuccess], "true", StringComparison.OrdinalIgnoreCase);
            var threads = iThreads >= 0 && iThreads < parts.Length && int.TryParse(parts[iThreads], out var t) ? t : 0;
            samples.Add(new JtlSample(elapsed, success, DateTimeOffset.FromUnixTimeMilliseconds(ts), threads));
        }
        return samples;
    }
}

/// <summary>Runner de OWASP ZAP: escaneo baseline vía Docker y parseo de alertas.</summary>
public class ZapScanRunner : ITestRunner
{
    private readonly ISandboxedProcessExecutor _executor;

    public ZapScanRunner(ISandboxedProcessExecutor executor) => _executor = executor;

    public AutomationFramework Framework => AutomationFramework.OwaspZap;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (!context.Parameters.TryGetValue("targetUrl", out var targetUrl) || string.IsNullOrWhiteSpace(targetUrl))
        {
            // Los scripts para ZAP guardan la URL objetivo como "ruta de script".
            targetUrl = context.Scripts.FirstOrDefault()?.ScriptPath ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(targetUrl))
            return new RunnerOutcome(false, [], [], null,
                "No se configuró la URL objetivo del escaneo de seguridad.");

        var reportName = $"zap-{Guid.NewGuid():N}.json";
        // PreferHostDockerCli: ZAP ya aísla en su imagen oficial; no anidar sandbox Newman/Playwright.
        var run = await _executor.RunAsync(new ScriptExecutionRequest(
            context.TestRunId, "docker",
            $"run --rm -v \"{context.WorkingDirectory}:/zap/wrk\" ghcr.io/zaproxy/zaproxy:stable " +
            $"zap-baseline.py -t \"{targetUrl}\" -J {reportName} -I",
            context.WorkingDirectory, TimeSpan.FromMinutes(45), null, [reportName],
            PreferHostDockerCli: true), ct);

        var reportPath = Path.Combine(context.WorkingDirectory, reportName);
        if (!File.Exists(reportPath))
            return new RunnerOutcome(false, [], [], null,
                $"ZAP no generó reporte (exit {run.ExitCode}). {run.StandardError}");

        var findings = ParseZapReport(await File.ReadAllTextAsync(reportPath, ct));
        var evidence = new RunnerEvidence(EvidenceType.JsonReport, reportPath, "application/json",
            new FileInfo(reportPath).Length);
        var critical = findings.Count(f => f.Risk >= RiskLevel.High);

        var result = new RunnerResultItem(null, $"Escaneo de seguridad {targetUrl}",
            critical == 0 ? ResultStatus.Passed : ResultStatus.Failed, 0,
            critical == 0 ? null : $"{critical} hallazgos de riesgo alto o crítico.",
            null, JsonSerializer.Serialize(new { totalFindings = findings.Count, highOrCritical = critical }),
            [evidence]);

        return new RunnerOutcome(true, [result], findings, null, null);
    }

    private static List<RunnerSecurityFinding> ParseZapReport(string json)
    {
        var findings = new List<RunnerSecurityFinding>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("site", out var sites)) return findings;

        foreach (var site in sites.EnumerateArray())
        {
            if (!site.TryGetProperty("alerts", out var alerts)) continue;
            foreach (var alert in alerts.EnumerateArray())
            {
                var risk = alert.TryGetProperty("riskcode", out var rc) ? rc.GetString() switch
                {
                    "3" => RiskLevel.High,
                    "2" => RiskLevel.Medium,
                    "1" => RiskLevel.Low,
                    _ => RiskLevel.Informational
                } : RiskLevel.Informational;

                var name = alert.GetProperty("alert").GetString() ?? "Alerta";
                var category = CategorizeAlert(name);
                var instance = alert.TryGetProperty("instances", out var inst) && inst.GetArrayLength() > 0
                    ? inst[0] : default;

                findings.Add(new RunnerSecurityFinding(
                    name, risk, category,
                    instance.ValueKind == JsonValueKind.Object && instance.TryGetProperty("uri", out var uri) ? uri.GetString() : null,
                    instance.ValueKind == JsonValueKind.Object && instance.TryGetProperty("param", out var param) ? param.GetString() : null,
                    instance.ValueKind == JsonValueKind.Object && instance.TryGetProperty("evidence", out var ev) ? ev.GetString() : null,
                    alert.TryGetProperty("solution", out var sol) ? StripHtml(sol.GetString()) : null,
                    alert.TryGetProperty("cweid", out var cwe) ? cwe.GetString() : null));
            }
        }
        return findings;
    }

    private static string CategorizeAlert(string alertName)
    {
        var lower = alertName.ToLowerInvariant();
        if (lower.Contains("sql")) return "SQL Injection";
        if (lower.Contains("xss") || lower.Contains("cross site scripting")) return "XSS";
        if (lower.Contains("csrf") || lower.Contains("cross-site request")) return "CSRF";
        if (lower.Contains("cookie")) return "Cookies";
        if (lower.Contains("header") || lower.Contains("content security policy")
            || lower.Contains("x-frame") || lower.Contains("strict-transport")) return "Headers";
        return "General";
    }

    private static string? StripHtml(string? value)
        => value is null ? null : System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", string.Empty).Trim();
}

/// <summary>Resuelve el runner adecuado por framework o tipo de prueba.</summary>
/// <summary>
/// Resuelve el runner por framework indexando <see cref="ITestRunner.Framework"/> de las
/// instancias registradas en DI. Antes del Sprint 4 este resolvía con un <c>switch</c> +
/// <see cref="IServiceProvider"/> (localizador de servicios) que exigía editar esta clase cada
/// vez que se agregaba un runner nuevo — violación de OCP documentada en ADR-009. Ahora un
/// runner nuevo solo necesita implementar <see cref="ITestRunner"/> y registrarse como tal en
/// <c>DependencyInjection.cs</c>; esta clase no cambia.
/// </summary>
public class TestRunnerFactory : ITestRunnerFactory
{
    private readonly Dictionary<AutomationFramework, ITestRunner> _runnersByFramework;

    public TestRunnerFactory(IEnumerable<ITestRunner> runners)
        => _runnersByFramework = runners.ToDictionary(r => r.Framework);

    public ITestRunner Resolve(AutomationFramework framework)
        => _runnersByFramework.TryGetValue(framework, out var runner)
            ? runner
            : throw new NotSupportedException($"Framework no soportado: {framework}");

    public ITestRunner ResolveByTestType(TestType testType) => testType switch
    {
        TestType.Api => Resolve(AutomationFramework.Postman),
        TestType.Performance => Resolve(AutomationFramework.JMeter),
        TestType.Security => Resolve(AutomationFramework.OwaspZap),
        TestType.Visual => Resolve(AutomationFramework.VisualRegression),
        // Funcionales, regresión, smoke y E2E se ejecutan con Playwright.
        _ => Resolve(AutomationFramework.Playwright)
    };
}
