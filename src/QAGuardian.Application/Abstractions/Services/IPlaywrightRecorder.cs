namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Spec de Playwright grabada o generada como andamiaje.</summary>
/// <param name="Content">Código de la spec (TypeScript / Playwright Test).</param>
/// <param name="FromCodegen">True si proviene de una sesión interactiva de <c>playwright codegen</c>.</param>
/// <param name="Note">Mensaje informativo (p. ej. si se usó andamiaje por no haber sesión interactiva).</param>
public record RecordedScript(string Content, bool FromCodegen, string? Note);

/// <summary>
/// Puerto: grabador de pruebas de Playwright. En una instalación local abre una sesión
/// interactiva de <c>playwright codegen</c> y devuelve la spec resultante; si no hay
/// entorno gráfico disponible, devuelve un andamiaje inicial para la URL.
/// </summary>
public interface IPlaywrightRecorder
{
    Task<RecordedScript> RecordAsync(string url, string workingDirectory, CancellationToken ct = default);
}
