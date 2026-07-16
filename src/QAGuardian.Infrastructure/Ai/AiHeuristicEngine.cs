using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Ai;

/// <summary>
/// Motor heurístico (sin LLM): fallback sin API key y baseline de benchmark Sprint 7.
/// </summary>
public static class AiHeuristicEngine
{
    public static AiDiagnosisDto Diagnose(FailureContext context)
    {
        var error = (context.ErrorMessage ?? string.Empty).ToLowerInvariant();
        var (cause, criticality, owner, confidence, quote) = Classify(error, context.ErrorMessage);

        return new AiDiagnosisDto(
            $"La prueba '{context.TestName}' falló: {context.ErrorMessage ?? "sin mensaje de error"}.",
            cause,
            criticality,
            "Revisar el detalle del error y las evidencias adjuntas; reproducir localmente antes de corregir.",
            criticality >= RiskLevel.High ? DefectPriority.High : DefectPriority.Medium,
            criticality >= RiskLevel.High ? 4m : 2m,
            owner,
            "heuristic-fallback",
            confidence,
            quote,
            AiFailureFingerprint.EstimateTokens(context.ErrorMessage) +
            AiFailureFingerprint.EstimateTokens(context.StackTrace) +
            AiFailureFingerprint.EstimateTokens(context.LogsExcerpt));
    }

    public static GeneratedTestsDto GenerateTests(
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<string>? existingCatalog = null,
        int maxTests = 6)
    {
        var areas = changedFiles
            .Select(f => f.Split('/', '\\').FirstOrDefault() ?? f)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        var existing = existingCatalog ?? [];
        var cases = new List<GeneratedTestCase>();

        if (changedFiles.Any(f => f.Contains("controller", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("/api/", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("\\api\\", StringComparison.OrdinalIgnoreCase)))
        {
            cases.Add(new GeneratedTestCase(
                AvoidDuplicateTitle("Validar contratos de API impactados por el PR", existing),
                AutomationFramework.Postman,
                "// Collection: status 200, schema y p95 < 800ms en endpoints modificados",
                "Se modificaron controladores/endpoints de API."));
        }

        if (changedFiles.Any(f => f.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("component", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("page", StringComparison.OrdinalIgnoreCase)))
        {
            cases.Add(new GeneratedTestCase(
                AvoidDuplicateTitle("Verificar renderizado y flujo de las vistas modificadas", existing),
                AutomationFramework.Playwright,
                "// Spec: navegar a vistas afectadas, validar elementos clave y screenshot",
                "Se modificaron componentes de interfaz de usuario."));
        }

        if (cases.Count == 0)
        {
            cases.Add(new GeneratedTestCase(
                AvoidDuplicateTitle("Smoke test de regresión sobre las áreas impactadas", existing),
                AutomationFramework.Playwright,
                "// Spec: flujo principal de extremo a extremo",
                "Cambios generales; smoke test recomendado."));
        }

        return new GeneratedTestsDto(cases.Take(maxTests).ToList(), areas);
    }

    /// <summary>Clasificación usada también por el benchmark de precisión.</summary>
    public static (string Cause, RiskLevel Criticality, string Owner, double Confidence, string? Quote)
        Classify(string errorLower, string? originalError)
    {
        return errorLower switch
        {
            var e when e.Contains("timeout") || e.Contains("timed out")
                => ("Tiempo de espera agotado: posible lentitud del ambiente o selector inestable.",
                    RiskLevel.Medium, "DevOps", 0.72, PickQuote(originalError, "timeout", "timed out")),
            var e when e.Contains("selector") || e.Contains("locator") || e.Contains("element")
                => ("Selector o elemento no encontrado: cambio en la interfaz de usuario.",
                    RiskLevel.Medium, "QA", 0.78, PickQuote(originalError, "selector", "locator", "element")),
            var e when e.Contains("500") || e.Contains("internal server")
                => ("Error interno del servidor durante la prueba.",
                    RiskLevel.High, "Desarrollador", 0.85, PickQuote(originalError, "500", "internal server")),
            var e when e.Contains("401") || e.Contains("403") || e.Contains("unauthorized")
                => ("Fallo de autenticación/autorización en el ambiente de pruebas.",
                    RiskLevel.High, "DevOps", 0.82, PickQuote(originalError, "401", "403", "unauthorized")),
            var e when e.Contains("sql") || e.Contains("database") || e.Contains("deadlock")
                => ("Error de base de datos detectado.",
                    RiskLevel.High, "Desarrollador", 0.8, PickQuote(originalError, "sql", "database", "deadlock")),
            var e when e.Contains("assert")
                => ("Aserción fallida: el comportamiento no coincide con lo esperado.",
                    RiskLevel.Medium, "Desarrollador", 0.7, PickQuote(originalError, "assert")),
            _ => ("Causa no determinada automáticamente; requiere revisión manual.",
                RiskLevel.Medium, "QA", 0.35, null)
        };
    }

    private static string AvoidDuplicateTitle(string title, IReadOnlyList<string> existing)
    {
        if (!existing.Any(t => t.Contains(title, StringComparison.OrdinalIgnoreCase)))
            return title;
        return $"{title} (regresión PR)";
    }

    private static string? PickQuote(string? original, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(original)) return null;
        foreach (var n in needles)
        {
            var idx = original.IndexOf(n, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var start = Math.Max(0, idx - 20);
            var len = Math.Min(original.Length - start, 80);
            return original.Substring(start, len).Trim();
        }
        return original.Length <= 80 ? original : original[..80];
    }
}
