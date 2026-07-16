using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Ai;

/// <summary>
/// Huella estable de un fallo para caché de diagnósticos (reduce tokens/costo en fallos repetidos).
/// </summary>
public static partial class AiFailureFingerprint
{
    public static string Compute(FailureContext context)
    {
        var error = Normalize(context.ErrorMessage);
        var stackTop = ExtractStackTop(context.StackTrace);
        var raw = $"{context.TestType}|{error}|{stackTop}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }

    public static string CacheKey(FailureContext context) => $"ai:diag:{Compute(context)}";

    /// <summary>Estimación barata de tokens (~4 chars/token) para métricas de costo.</summary>
    public static int EstimateTokens(string? text)
        => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    public static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        if (value.Length <= max) return value;
        return value[..max] + "…";
    }

    private static string Normalize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        var s = message.Trim().ToLowerInvariant();
        s = GuidRegex().Replace(s, "{id}");
        s = HexRegex().Replace(s, "{hex}");
        s = NumberRegex().Replace(s, "{n}");
        s = WhitespaceRegex().Replace(s, " ");
        return s.Length <= 240 ? s : s[..240];
    }

    private static string ExtractStackTop(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace)) return "";
        var line = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Contains("at ", StringComparison.Ordinal) || l.Contains("→", StringComparison.Ordinal))
            ?? stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? "";
        return Normalize(line.Length <= 160 ? line : line[..160]);
    }

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\b[0-9a-f]{16,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex HexRegex();

    // Sin \b: mensajes tipo "30000ms" no tienen frontera de palabra entre dígitos y unidad.
    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
