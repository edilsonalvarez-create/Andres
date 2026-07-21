namespace QAGuardian.Infrastructure.Runners;

/// <summary>Utilidades compartidas para paths relativos al workspace del sandbox.</summary>
internal static class RunnerSandboxPaths
{
    public static string ToWorkspaceRelative(string path, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(workingDirectory);
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var rel = Path.GetRelativePath(root, full).Replace('\\', '/');
            return string.IsNullOrEmpty(rel) || rel == "." ? Path.GetFileName(full) : rel;
        }

        return Path.GetFileName(path);
    }

    /// <summary>
    /// Cita un argumento para <see cref="ProcessExecutor"/> / <c>docker run</c>
    /// (UseShellExecute=false). Evita que espacios o comillas en valores controlados
    /// por el usuario (p. ej. targetUrl ZAP) se partan en tokens adicionales.
    /// </summary>
    public static string QuoteArg(string value)
    {
        value ??= string.Empty;
        if (value.Contains('"', StringComparison.Ordinal))
            value = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{value}\"";
    }

    public static string QuoteJoin(IEnumerable<string> paths, string workingDirectory, bool relative)
        => string.Join(' ', paths.Select(p =>
            QuoteArg(relative ? ToWorkspaceRelative(p, workingDirectory) : p)));
}
