namespace QAGuardian.Infrastructure.Ai;

/// <summary>Opciones del motor IA (costo, tokens, umbrales anti-alucinación).</summary>
public sealed class AnthropicAiOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = "";

    /// <summary>Modelo por defecto: Sonnet (mejor costo/calidad para JSON estructurado que Opus).</summary>
    public string Model { get; set; } = "claude-sonnet-4-5";

    public int MaxTokensDiagnosis { get; set; } = 1024;
    public int MaxTokensGeneration { get; set; } = 2048;

    /// <summary>Thinking adaptativo aumenta mucho el costo; desactivado por defecto en Sprint 7.</summary>
    public bool EnableThinking { get; set; }

    public int CacheMinutes { get; set; } = 120;
    public double AutoDefectMinConfidence { get; set; } = 0.75;
    public int StackTraceMaxChars { get; set; } = 2000;
    public int LogsMaxChars { get; set; } = 1500;
    public int SqlMaxChars { get; set; } = 800;
    public int DiffMaxChars { get; set; } = 6000;
    public int MaxFailuresPerRun { get; set; } = 5;
    public int MaxGeneratedTests { get; set; } = 6;
}
