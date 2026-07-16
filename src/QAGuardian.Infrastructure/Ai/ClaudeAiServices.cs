using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Ai;

/// <summary>
/// Diagnóstico de fallos con Anthropic. Sprint 7: modelo configurable (Sonnet por defecto),
/// prompts compactos, sin thinking por defecto, caché por fingerprint y confidence anti-alucinación.
/// </summary>
public class ClaudeAiAnalysisService : IAiAnalysisService
{
    private readonly AnthropicClient? _client;
    private readonly ICacheService _cache;
    private readonly AnthropicAiOptions _options;
    private readonly ILogger<ClaudeAiAnalysisService> _logger;

    public ClaudeAiAnalysisService(
        IOptions<AnthropicAiOptions> options,
        ICacheService cache,
        ILogger<ClaudeAiAnalysisService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _client = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? null
            : new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<AiDiagnosisDto> AnalyzeFailureAsync(FailureContext context, CancellationToken ct = default)
    {
        var cacheKey = AiFailureFingerprint.CacheKey(context);
        var cached = await _cache.GetAsync<AiDiagnosisDto>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogInformation("Diagnóstico IA servido desde caché ({Fingerprint}).", AiFailureFingerprint.Compute(context));
            return cached with { ModelUsed = $"{cached.ModelUsed}+cache" };
        }

        if (_client is null)
        {
            _logger.LogInformation("Anthropic:ApiKey no configurada; usando diagnóstico heurístico.");
            var heuristic = AiHeuristicEngine.Diagnose(context);
            await _cache.SetAsync(cacheKey, heuristic, TimeSpan.FromMinutes(_options.CacheMinutes), ct);
            return heuristic;
        }

        try
        {
            var stack = AiFailureFingerprint.Truncate(context.StackTrace, _options.StackTraceMaxChars);
            var logs = AiFailureFingerprint.Truncate(context.LogsExcerpt, _options.LogsMaxChars);
            var sql = AiFailureFingerprint.Truncate(context.SqlQuery, _options.SqlMaxChars);

            // Prompt compacto: menos tokens, exige evidencia citada (reduce alucinaciones).
            var prompt =
                $"""
                Diagnostica este fallo de prueba. Sé breve y cíta evidencia literal del error/stack/logs.

                Proyecto: {context.ProjectName}
                Tipo: {context.TestType}
                Prueba: {context.TestName}
                Error: {context.ErrorMessage ?? "(n/d)"}
                Stack (top): {stack ?? "(n/d)"}
                Logs: {logs ?? "(n/d)"}
                SQL: {sql ?? "(n/d)"}

                Reglas:
                - Responde en español, frases cortas.
                - evidenceQuote: fragmento literal del error/stack/logs (máx 120 chars) o vacío si no hay.
                - confidence: 0.0–1.0 (baja si falta evidencia).
                - criticality: 0=Info,1=Bajo,2=Medio,3=Alto,4=Crítico.
                - suggestedPriority: 1=Baja,2=Media,3=Alta,4=Urgente.
                - suggestedOwnerRole: Desarrollador|QA|DevOps|LiderTecnico.
                - No inventes detalles ausentes en el contexto.
                """;

            var estimatedPromptTokens = AiFailureFingerprint.EstimateTokens(prompt);
            MessageCreateParams createParams = _options.EnableThinking
                ? new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokensDiagnosis,
                    Thinking = new ThinkingConfigAdaptive(),
                    System = "Ingeniero QA senior. Diagnósticos concisos, anclados en evidencia. Sin especulación.",
                    OutputConfig = new OutputConfig { Format = BuildDiagnosisFormat() },
                    Messages = [new() { Role = Role.User, Content = prompt }]
                }
                : new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokensDiagnosis,
                    System = "Ingeniero QA senior. Diagnósticos concisos, anclados en evidencia. Sin especulación.",
                    OutputConfig = new OutputConfig { Format = BuildDiagnosisFormat() },
                    Messages = [new() { Role = Role.User, Content = prompt }]
                };

            var response = await _client.Messages.Create(createParams, cancellationToken: ct);

            var json = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();
            if (json is null)
            {
                var fallback = AiHeuristicEngine.Diagnose(context);
                await _cache.SetAsync(cacheKey, fallback, TimeSpan.FromMinutes(_options.CacheMinutes), ct);
                return fallback;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var confidence = root.TryGetProperty("confidence", out var confEl)
                ? Math.Clamp(confEl.GetDouble(), 0, 1)
                : 0.5;
            var evidence = root.TryGetProperty("evidenceQuote", out var evEl)
                ? AiFailureFingerprint.Truncate(evEl.GetString(), 120)
                : null;

            // Si el modelo no cita evidencia, bajamos confianza (anti-alucinación).
            if (string.IsNullOrWhiteSpace(evidence))
                confidence = Math.Min(confidence, 0.55);

            var diagnosis = new AiDiagnosisDto(
                root.GetProperty("diagnosis").GetString() ?? "Sin diagnóstico",
                root.GetProperty("probableCause").GetString() ?? "Desconocida",
                (RiskLevel)root.GetProperty("criticality").GetInt32(),
                root.GetProperty("recommendation").GetString() ?? "Revisar manualmente",
                (DefectPriority)root.GetProperty("suggestedPriority").GetInt32(),
                root.GetProperty("estimatedHours").GetDecimal(),
                root.GetProperty("suggestedOwnerRole").GetString() ?? "QA",
                _options.Model,
                confidence,
                evidence,
                estimatedPromptTokens);

            _logger.LogInformation(
                "Diagnóstico IA listo. Model={Model} PromptTokens~{Tokens} Confidence={Confidence:F2}",
                _options.Model, estimatedPromptTokens, confidence);

            await _cache.SetAsync(cacheKey, diagnosis, TimeSpan.FromMinutes(_options.CacheMinutes), ct);
            return diagnosis;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "El agente IA falló; usando diagnóstico heurístico.");
            var fallback = AiHeuristicEngine.Diagnose(context);
            await _cache.SetAsync(cacheKey, fallback, TimeSpan.FromMinutes(_options.CacheMinutes), ct);
            return fallback;
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
                },
                ["confidence"] = new Dictionary<string, object> { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                ["evidenceQuote"] = new { type = "string" }
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[]
            {
                "diagnosis", "probableCause", "criticality", "recommendation",
                "suggestedPriority", "estimatedHours", "suggestedOwnerRole",
                "confidence", "evidenceQuote"
            }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
        }
    };
}

/// <summary>
/// Generación de pruebas para PRs. Sprint 7: grounding con diff + catálogo existente, menos tokens.
/// </summary>
public class ClaudeTestGenerationService : IAiTestGenerationService
{
    private readonly AnthropicClient? _client;
    private readonly AnthropicAiOptions _options;
    private readonly ILogger<ClaudeTestGenerationService> _logger;

    public ClaudeTestGenerationService(
        IOptions<AnthropicAiOptions> options,
        ILogger<ClaudeTestGenerationService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? null
            : new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<GeneratedTestsDto> GenerateTestsForChangesAsync(
        TestGenerationRequest request, CancellationToken ct = default)
    {
        if (_client is null)
            return AiHeuristicEngine.GenerateTests(request.ChangedFiles, request.ExistingCatalog, _options.MaxGeneratedTests);

        try
        {
            var files = request.ChangedFiles.Take(40).ToList();
            var diff = AiFailureFingerprint.Truncate(request.DiffExcerpt, _options.DiffMaxChars);
            var catalog = (request.ExistingCatalog ?? [])
                .Take(25)
                .Select(t => $"- {t}")
                .ToList();

            var prompt =
                $"""
                Genera hasta {_options.MaxGeneratedTests} casos de prueba para el impacto del PR.
                Ancla cada caso en el diff/archivos. No inventes endpoints ni pantallas ausentes.
                Evita duplicar títulos del catálogo existente.

                Proyecto: {request.ProjectName}
                Archivos:
                {string.Join('\n', files.Select(f => $"- {f}"))}

                Diff:
                {diff ?? "(diff no disponible — usa solo nombres de archivo y sé conservador)"}

                Catálogo existente (no duplicar):
                {(catalog.Count == 0 ? "(vacío)" : string.Join('\n', catalog))}

                framework: 1=Playwright, 2=Postman.
                suggestedScript breve. rationale en español, 1 frase.
                """;

            MessageCreateParams createParams = _options.EnableThinking
                ? new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokensGeneration,
                    Thinking = new ThinkingConfigAdaptive(),
                    System = "Arquitecto de automatización QA. Casos precisos, anclados al diff. Sin alucinaciones.",
                    OutputConfig = new OutputConfig { Format = BuildGenerationFormat() },
                    Messages = [new() { Role = Role.User, Content = prompt }]
                }
                : new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = _options.MaxTokensGeneration,
                    System = "Arquitecto de automatización QA. Casos precisos, anclados al diff. Sin alucinaciones.",
                    OutputConfig = new OutputConfig { Format = BuildGenerationFormat() },
                    Messages = [new() { Role = Role.User, Content = prompt }]
                };

            var response = await _client.Messages.Create(createParams, cancellationToken: ct);

            var json = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();
            if (json is null)
                return AiHeuristicEngine.GenerateTests(request.ChangedFiles, request.ExistingCatalog, _options.MaxGeneratedTests);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cases = root.GetProperty("testCases").EnumerateArray()
                .Select(tc => new GeneratedTestCase(
                    tc.GetProperty("title").GetString() ?? "Caso generado",
                    (AutomationFramework)tc.GetProperty("framework").GetInt32(),
                    tc.GetProperty("suggestedScript").GetString() ?? "",
                    tc.GetProperty("rationale").GetString() ?? ""))
                .Take(_options.MaxGeneratedTests)
                .ToList();
            var areas = root.GetProperty("impactedAreas").EnumerateArray()
                .Select(a => a.GetString() ?? "")
                .Where(a => a.Length > 0)
                .ToList();

            _logger.LogInformation(
                "Generación IA: {Count} casos, PromptTokens~{Tokens}, Model={Model}",
                cases.Count, AiFailureFingerprint.EstimateTokens(prompt), _options.Model);

            return new GeneratedTestsDto(cases, areas);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "La generación de pruebas con IA falló; usando heurística.");
            return AiHeuristicEngine.GenerateTests(request.ChangedFiles, request.ExistingCatalog, _options.MaxGeneratedTests);
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
}
