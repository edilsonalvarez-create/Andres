using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Ai;

/// <summary>
/// Agente IA de QA Guardian sobre la Messages API de Anthropic (claude-opus-4-8)
/// con salida estructurada JSON. Si no hay API key configurada, degrada a un
/// diagnóstico heurístico para no bloquear el pipeline.
/// </summary>
public class ClaudeAiAnalysisService : IAiAnalysisService
{
    private readonly AnthropicClient? _client;
    private readonly ILogger<ClaudeAiAnalysisService> _logger;
    private const string ModelId = "claude-opus-4-8";

    public ClaudeAiAnalysisService(IConfiguration configuration, ILogger<ClaudeAiAnalysisService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Anthropic:ApiKey"];
        _client = string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<AiDiagnosisDto> AnalyzeFailureAsync(FailureContext context, CancellationToken ct = default)
    {
        if (_client is null)
        {
            _logger.LogInformation("Anthropic:ApiKey no configurada; usando diagnóstico heurístico.");
            return HeuristicDiagnosis(context);
        }

        try
        {
            var prompt =
                $"""
                Analiza el siguiente fallo de una prueba automatizada y genera un diagnóstico.

                Proyecto: {context.ProjectName}
                Tipo de prueba: {context.TestType}
                Prueba: {context.TestName}
                Mensaje de error: {context.ErrorMessage ?? "(no disponible)"}
                StackTrace: {Truncate(context.StackTrace) ?? "(no disponible)"}
                Extracto de logs: {Truncate(context.LogsExcerpt) ?? "(no disponible)"}
                Consulta SQL relacionada: {context.SqlQuery ?? "(no disponible)"}

                Responde en español. criticality: 0=Informativo,1=Bajo,2=Medio,3=Alto,4=Crítico.
                suggestedPriority: 1=Baja,2=Media,3=Alta,4=Urgente.
                suggestedOwnerRole debe ser uno de: Desarrollador, QA, DevOps, LiderTecnico.
                """;

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = ModelId,
                MaxTokens = 2048,
                Thinking = new ThinkingConfigAdaptive(),
                System = "Eres un ingeniero senior de QA que diagnostica fallos de pruebas automatizadas.",
                OutputConfig = new OutputConfig { Format = BuildDiagnosisFormat() },
                Messages = [new() { Role = Role.User, Content = prompt }]
            }, cancellationToken: ct);

            var json = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();
            if (json is null) return HeuristicDiagnosis(context);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new AiDiagnosisDto(
                root.GetProperty("diagnosis").GetString() ?? "Sin diagnóstico",
                root.GetProperty("probableCause").GetString() ?? "Desconocida",
                (RiskLevel)root.GetProperty("criticality").GetInt32(),
                root.GetProperty("recommendation").GetString() ?? "Revisar manualmente",
                (DefectPriority)root.GetProperty("suggestedPriority").GetInt32(),
                root.GetProperty("estimatedHours").GetDecimal(),
                root.GetProperty("suggestedOwnerRole").GetString() ?? "QA",
                ModelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El agente IA falló; usando diagnóstico heurístico.");
            return HeuristicDiagnosis(context);
        }
    }

    private static JsonOutputFormat BuildDiagnosisFormat() => new()
    {
        Schema = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["diagnosis"] = new { type = "string" },
                ["probableCause"] = new { type = "string" },
                ["criticality"] = new Dictionary<string, object> { ["type"] = "integer", ["enum"] = new[] { 0, 1, 2, 3, 4 } },
                ["recommendation"] = new { type = "string" },
                ["suggestedPriority"] = new Dictionary<string, object> { ["type"] = "integer", ["enum"] = new[] { 1, 2, 3, 4 } },
                ["estimatedHours"] = new { type = "number" },
                ["suggestedOwnerRole"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "Desarrollador", "QA", "DevOps", "LiderTecnico" }
                }
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[]
            {
                "diagnosis", "probableCause", "criticality", "recommendation",
                "suggestedPriority", "estimatedHours", "suggestedOwnerRole"
            }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
        }
    };

    /// <summary>Diagnóstico basado en reglas cuando el agente IA no está disponible.</summary>
    private static AiDiagnosisDto HeuristicDiagnosis(FailureContext context)
    {
        var error = (context.ErrorMessage ?? string.Empty).ToLowerInvariant();
        var (cause, criticality, owner) = error switch
        {
            var e when e.Contains("timeout") || e.Contains("timed out")
                => ("Tiempo de espera agotado: posible lentitud del ambiente o selector inestable.", RiskLevel.Medium, "DevOps"),
            var e when e.Contains("selector") || e.Contains("locator") || e.Contains("element")
                => ("Selector o elemento no encontrado: cambio en la interfaz de usuario.", RiskLevel.Medium, "QA"),
            var e when e.Contains("500") || e.Contains("internal server")
                => ("Error interno del servidor durante la prueba.", RiskLevel.High, "Desarrollador"),
            var e when e.Contains("401") || e.Contains("403") || e.Contains("unauthorized")
                => ("Fallo de autenticación/autorización en el ambiente de pruebas.", RiskLevel.High, "DevOps"),
            var e when e.Contains("sql") || e.Contains("database") || e.Contains("deadlock")
                => ("Error de base de datos detectado.", RiskLevel.High, "Desarrollador"),
            var e when e.Contains("assert")
                => ("Aserción fallida: el comportamiento no coincide con lo esperado.", RiskLevel.Medium, "Desarrollador"),
            _ => ("Causa no determinada automáticamente; requiere revisión manual.", RiskLevel.Medium, "QA")
        };

        return new AiDiagnosisDto(
            $"La prueba '{context.TestName}' falló: {context.ErrorMessage ?? "sin mensaje de error"}.",
            cause, criticality,
            "Revisar el detalle del error y las evidencias adjuntas; reproducir localmente antes de corregir.",
            criticality >= RiskLevel.High ? DefectPriority.High : DefectPriority.Medium,
            criticality >= RiskLevel.High ? 4m : 2m,
            owner,
            "heuristic-fallback");
    }

    private static string? Truncate(string? value, int max = 4000)
        => value is null ? null : value.Length <= max ? value : value[..max] + "…";
}

/// <summary>Agente IA que genera casos de prueba a partir de los archivos modificados en un PR.</summary>
public class ClaudeTestGenerationService : IAiTestGenerationService
{
    private readonly AnthropicClient? _client;
    private readonly ILogger<ClaudeTestGenerationService> _logger;
    private const string ModelId = "claude-opus-4-8";

    public ClaudeTestGenerationService(IConfiguration configuration, ILogger<ClaudeTestGenerationService> logger)
    {
        _logger = logger;
        var apiKey = configuration["Anthropic:ApiKey"];
        _client = string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<GeneratedTestsDto> GenerateTestsForChangesAsync(
        string projectName, IReadOnlyList<string> changedFiles, string? diffExcerpt, CancellationToken ct = default)
    {
        if (_client is null)
            return HeuristicGeneration(changedFiles);

        try
        {
            var prompt =
                $"""
                Proyecto: {projectName}
                Archivos modificados en el Pull Request:
                {string.Join('\n', changedFiles.Select(f => $"- {f}"))}

                {(diffExcerpt is null ? "" : $"Extracto del diff:\n{diffExcerpt}")}

                Genera casos de prueba (máximo 8) para cubrir el impacto de estos cambios.
                framework: 1=Playwright (UI/E2E), 2=Postman (API).
                Para cada caso incluye un script sugerido breve y la justificación en español.
                """;

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = ModelId,
                MaxTokens = 4096,
                Thinking = new ThinkingConfigAdaptive(),
                System = "Eres un arquitecto de automatización QA. Diseñas casos de prueba precisos y relevantes.",
                OutputConfig = new OutputConfig { Format = BuildGenerationFormat() },
                Messages = [new() { Role = Role.User, Content = prompt }]
            }, cancellationToken: ct);

            var json = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();
            if (json is null) return HeuristicGeneration(changedFiles);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cases = root.GetProperty("testCases").EnumerateArray()
                .Select(tc => new GeneratedTestCase(
                    tc.GetProperty("title").GetString() ?? "Caso generado",
                    (AutomationFramework)tc.GetProperty("framework").GetInt32(),
                    tc.GetProperty("suggestedScript").GetString() ?? "",
                    tc.GetProperty("rationale").GetString() ?? ""))
                .ToList();
            var areas = root.GetProperty("impactedAreas").EnumerateArray()
                .Select(a => a.GetString() ?? "")
                .Where(a => a.Length > 0)
                .ToList();
            return new GeneratedTestsDto(cases, areas);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "La generación de pruebas con IA falló; usando heurística.");
            return HeuristicGeneration(changedFiles);
        }
    }

    private static JsonOutputFormat BuildGenerationFormat() => new()
    {
        Schema = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["impactedAreas"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["type"] = "string" }
                },
                ["testCases"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["title"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["framework"] = new Dictionary<string, object> { ["type"] = "integer", ["enum"] = new[] { 1, 2 } },
                            ["suggestedScript"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["rationale"] = new Dictionary<string, object> { ["type"] = "string" }
                        },
                        ["required"] = new[] { "title", "framework", "suggestedScript", "rationale" },
                        ["additionalProperties"] = false
                    }
                }
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "impactedAreas", "testCases" }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
        }
    };

    private static GeneratedTestsDto HeuristicGeneration(IReadOnlyList<string> changedFiles)
    {
        var areas = changedFiles
            .Select(f => f.Split('/').FirstOrDefault() ?? f)
            .Distinct()
            .Take(10)
            .ToList();

        var cases = new List<GeneratedTestCase>();
        if (changedFiles.Any(f => f.Contains("controller", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("api", StringComparison.OrdinalIgnoreCase)))
            cases.Add(new GeneratedTestCase(
                "Validar contratos de API impactados por el PR",
                AutomationFramework.Postman,
                "// Collection sugerida: validar status 200, esquema y tiempos < 800ms en endpoints modificados",
                "Se modificaron controladores/endpoints de API."));

        if (changedFiles.Any(f => f.EndsWith(".tsx") || f.EndsWith(".jsx")
                                  || f.Contains("component", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("page", StringComparison.OrdinalIgnoreCase)))
            cases.Add(new GeneratedTestCase(
                "Verificar renderizado y flujo de las vistas modificadas",
                AutomationFramework.Playwright,
                "// Spec sugerida: navegar a las vistas afectadas, validar elementos clave y capturar screenshot",
                "Se modificaron componentes de interfaz de usuario."));

        if (cases.Count == 0)
            cases.Add(new GeneratedTestCase(
                "Smoke test de regresión sobre las áreas impactadas",
                AutomationFramework.Playwright,
                "// Spec sugerida: ejecutar el flujo principal de la aplicación de extremo a extremo",
                "Cambios generales sin patrón identificable; se recomienda smoke test."));

        return new GeneratedTestsDto(cases, areas);
    }
}
