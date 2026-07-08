using Microsoft.Extensions.Configuration;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Storage;

/// <summary>Almacenamiento de evidencias en sistema de archivos local (extensible a S3/Azure Blob).</summary>
public class FileEvidenceStorage : IEvidenceStorage
{
    private readonly string _rootPath;

    public FileEvidenceStorage(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:EvidencePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage", "evidence");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        var safePath = GetSafeFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(safePath)!);
        await using var file = File.Create(safePath);
        await content.CopyToAsync(file, ct);
        return Path.GetRelativePath(_rootPath, safePath).Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var safePath = GetSafeFullPath(path);
        if (!File.Exists(safePath))
            throw new FileNotFoundException("La evidencia solicitada no existe.", path);
        return Task.FromResult<Stream>(File.OpenRead(safePath));
    }

    public string GetAbsolutePath(string relativePath) => GetSafeFullPath(relativePath);

    public string CreateRunDirectory(Guid testRunId)
    {
        var dir = Path.Combine(_rootPath, "runs", testRunId.ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Previene path traversal: toda ruta debe resolver dentro del directorio raíz.</summary>
    private string GetSafeFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_rootPath, relativePath));
        if (!fullPath.StartsWith(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Ruta de evidencia fuera del directorio permitido.");
        return fullPath;
    }
}
