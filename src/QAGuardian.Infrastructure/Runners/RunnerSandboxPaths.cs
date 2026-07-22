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

    public static string QuoteJoin(IEnumerable<string> paths, string workingDirectory, bool relative)
        => string.Join(' ', paths.Select(p =>
            $"\"{(relative ? ToWorkspaceRelative(p, workingDirectory) : p)}\""));
}
