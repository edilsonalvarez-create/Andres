using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>
/// Graba specs de Playwright con <c>npx playwright codegen</c> (sesión interactiva en
/// instalaciones locales con entorno gráfico). Si la grabación no produce una spec
/// (servidor headless o sesión cancelada), devuelve un andamiaje inicial para la URL.
/// </summary>
public class PlaywrightRecorder : IPlaywrightRecorder
{
    private readonly ProcessExecutor _executor;
    private readonly ILogger<PlaywrightRecorder> _logger;

    public PlaywrightRecorder(ProcessExecutor executor, ILogger<PlaywrightRecorder> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<RecordedScript> RecordAsync(string url, string workingDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(workingDirectory);
        var outputPath = Path.Combine(workingDirectory, $"codegen-{Guid.NewGuid():N}.spec.ts");

        try
        {
            // codegen abre un navegador; la spec se escribe al cerrarlo. Timeout amplio para
            // dar tiempo a grabar. En un servidor sin entorno gráfico falla y se usa el andamiaje.
            var run = await _executor.RunAsync("npx",
                $"playwright codegen --target=playwright-test -o \"{outputPath}\" \"{url}\"",
                workingDirectory, TimeSpan.FromMinutes(10), null, ct);

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
