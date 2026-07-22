using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Graba specs de Playwright con <c>npx playwright codegen</c> vía sandbox.
/// En headless/sandbox suele fallar (requiere entorno gráfico) y se usa andamiaje.
/// </summary>
public class PlaywrightRecorder : IPlaywrightRecorder
{
    private readonly ISandboxedProcessExecutor _executor;
    private readonly RunnerSandboxOptions _options;
    private readonly ILogger<PlaywrightRecorder> _logger;

    public PlaywrightRecorder(
        ISandboxedProcessExecutor executor,
        IOptions<RunnerSandboxOptions> options,
        ILogger<PlaywrightRecorder> logger)
    {
        _executor = executor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RecordedScript> RecordAsync(string url, string workingDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(workingDirectory);
        var outputFile = $"codegen-{Guid.NewGuid():N}.spec.ts";
        var outputPath = Path.Combine(workingDirectory, outputFile);
        var sandbox = _executor.UsesContainerSandbox;
        var outArg = sandbox ? outputFile : outputPath;

        try
        {
            var run = await _executor.RunAsync(new ScriptExecutionRequest(
                Guid.Empty, "npx",
                $"playwright codegen --target=playwright-test -o \"{outArg}\" \"{url}\"",
                workingDirectory, TimeSpan.FromMinutes(10), null, [outputFile],
                sandbox ? _options.SandboxImagePlaywright : null), ct);

            if (File.Exists(outputPath))
            {
                var content = await File.ReadAllTextAsync(outputPath, ct);
                if (!string.IsNullOrWhiteSpace(content))
                    return new RecordedScript(content, FromCodegen: true, Note: null);
            }

            _logger.LogInformation("codegen no produjo spec (exit {Exit}); se usa andamiaje.", run.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar playwright codegen; se usa andamiaje.");
        }

        return new RecordedScript(
            Scaffold(url),
            FromCodegen: false,
            Note: "No se pudo iniciar una sesión de grabación interactiva (requiere entorno gráfico). " +
                  "Se generó un andamiaje inicial; edítelo y guárdelo. Para grabar de forma interactiva " +
                  "ejecute localmente: npx playwright codegen " + url);
    }

    private static string Scaffold(string url) =>
        $$"""
        import { test, expect } from '@playwright/test';

        test('Escenario en {{url}}', async ({ page }) => {
          await page.goto('{{url}}');

          // La página carga correctamente.
          await expect(page).toHaveTitle(/.+/);

          // TODO: agregue aquí las interacciones y aserciones del escenario.
          // Ejemplos:
          // await page.getByRole('button', { name: 'Iniciar sesión' }).click();
          // await expect(page.getByText('Bienvenido')).toBeVisible();

          // Evidencia visual del estado final.
          await page.screenshot({ path: 'final.png', fullPage: true });
        });
        """;
}
