using System.Text.Json;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using SixLabors.ImageSharp;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Runner de regresión visual: captura la pantalla actual de cada URL con Playwright,
/// la compara contra el baseline del proyecto (creándolo la primera vez) y adjunta
/// baseline, actual y diff como evidencia. Falla si el % de píxeles distintos supera el umbral.
/// Para casos de tipo Visual, <c>AutomationScriptPath</c> contiene la URL objetivo.
/// </summary>
public class VisualRegressionRunner : ITestRunner
{
    private const decimal DefaultThresholdPercent = 0.10m;
    private const int DefaultPixelTolerance = 30;

    private readonly ProcessExecutor _executor;
    private readonly IImageComparer _comparer;
    private readonly IEvidenceStorage _storage;
    private readonly IRepository<VisualBaseline> _baselines;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<VisualRegressionRunner> _logger;

    public VisualRegressionRunner(
        ProcessExecutor executor, IImageComparer comparer, IEvidenceStorage storage,
        IRepository<VisualBaseline> baselines, IUnitOfWork uow, ILogger<VisualRegressionRunner> logger)
    {
        _executor = executor;
        _comparer = comparer;
        _storage = storage;
        _baselines = baselines;
        _uow = uow;
        _logger = logger;
    }

    public AutomationFramework Framework => AutomationFramework.VisualRegression;

    public async Task<RunnerOutcome> ExecuteAsync(TestRunContext context, CancellationToken ct = default)
    {
        if (context.Scripts.Count == 0)
            return new RunnerOutcome(true, [], [], null,
                "No hay casos de prueba visuales configurados para este proyecto.");

        var results = new List<RunnerResultItem>();
        foreach (var script in context.Scripts)
        {
            var key = script.TestCaseId?.ToString("N") ?? Sanitize(script.Name);
            var url = script.ScriptPath;
            var actualPath = Path.Combine(context.WorkingDirectory, $"visual-{key}-actual.png");

            var capture = await CaptureAsync(url, actualPath, context.WorkingDirectory, ct);
            if (!capture)
            {
                results.Add(new RunnerResultItem(script.TestCaseId, script.Name, ResultStatus.Failed, 0,
                    $"No se pudo capturar la pantalla de '{url}'.", null, null, []));
                continue;
            }

            var (width, height) = ReadDimensions(actualPath);
            var baseline = (await _baselines.ListAsync(
                b => b.ProjectId == context.ProjectId && b.BaselineKey == key, ct)).FirstOrDefault();

            if (baseline is null)
            {
                results.Add(await CreateBaselineAsync(context, script, key, actualPath, width, height, ct));
                continue;
            }

            results.Add(Compare(context, script, key, baseline, actualPath, width, height));
        }

        return new RunnerOutcome(true, results, [], null, null);
    }

    private async Task<bool> CaptureAsync(string url, string outputPath, string workDir, CancellationToken ct)
    {
        // Playwright CLI captura la pantalla completa de una URL sin necesidad de una spec.
        var run = await _executor.RunAsync("npx",
            $"playwright screenshot --full-page \"{url}\" \"{outputPath}\"",
            workDir, TimeSpan.FromMinutes(3), null, ct);
        if (!File.Exists(outputPath))
        {
            _logger.LogWarning("Playwright no capturó {Url} (exit {Exit}): {Err}",
                url, run.ExitCode, run.StandardError);
            return false;
        }
        return true;
    }

    private async Task<RunnerResultItem> CreateBaselineAsync(TestRunContext context, TestScriptRef script,
        string key, string actualPath, int width, int height, CancellationToken ct)
    {
        // Primera ejecución del escenario: la captura actual se promueve a baseline.
        await using var stream = File.OpenRead(actualPath);
        var relativePath = await _storage.SaveAsync(
            $"baselines/{context.ProjectId:N}/{key}.png", stream, ct);

        var baseline = new VisualBaseline(context.ProjectId, key, relativePath,
            width, height, DefaultThresholdPercent, DefaultPixelTolerance);
        await _baselines.AddAsync(baseline, ct);
        await _uow.SaveChangesAsync(ct);

        var evidence = new RunnerEvidence(EvidenceType.Screenshot, actualPath, "image/png",
            new FileInfo(actualPath).Length);
        return new RunnerResultItem(script.TestCaseId, $"{script.Name} (baseline creado)",
            ResultStatus.Passed, 0, null, null,
            JsonSerializer.Serialize(new { baselineCreated = true, width, height }), [evidence]);
    }

    private RunnerResultItem Compare(TestRunContext context, TestScriptRef script, string key,
        VisualBaseline baseline, string actualPath, int width, int height)
    {
        var baselineAbs = _storage.GetAbsolutePath(baseline.BaselinePath);
        var baselineCopy = Path.Combine(context.WorkingDirectory, $"visual-{key}-baseline.png");
        var diffPath = Path.Combine(context.WorkingDirectory, $"visual-{key}-diff.png");
        File.Copy(baselineAbs, baselineCopy, overwrite: true);

        var comparison = _comparer.Compare(baselineAbs, actualPath, diffPath, baseline.PixelTolerance);
        var passed = comparison.SizeMatched
                     && (decimal)comparison.MismatchPercent <= baseline.ThresholdPercent;

        var evidences = new List<RunnerEvidence>
        {
            new(EvidenceType.Screenshot, baselineCopy, "image/png", new FileInfo(baselineCopy).Length),
            new(EvidenceType.Screenshot, actualPath, "image/png", new FileInfo(actualPath).Length),
            new(EvidenceType.Screenshot, diffPath, "image/png",
                File.Exists(diffPath) ? new FileInfo(diffPath).Length : 0)
        };

        var metrics = JsonSerializer.Serialize(new
        {
            mismatchPercent = comparison.MismatchPercent,
            thresholdPercent = baseline.ThresholdPercent,
            differentPixels = comparison.DifferentPixels,
            totalPixels = comparison.TotalPixels,
            sizeMatched = comparison.SizeMatched,
            baselineSize = new { baseline.Width, baseline.Height },
            actualSize = new { width, height }
        });

        var error = passed ? null
            : comparison.SizeMatched
                ? $"Cambio visual {comparison.MismatchPercent}% supera el umbral {baseline.ThresholdPercent}%."
                : $"Las dimensiones cambiaron: baseline {baseline.Width}x{baseline.Height}, actual {width}x{height}.";

        return new RunnerResultItem(script.TestCaseId, script.Name,
            passed ? ResultStatus.Passed : ResultStatus.Failed, 0, error, null, metrics, evidences);
    }

    private static (int Width, int Height) ReadDimensions(string path)
    {
        var info = Image.Identify(path);
        return (info.Width, info.Height);
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-').ToLowerInvariant();
    }
}
