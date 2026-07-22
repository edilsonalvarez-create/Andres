using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Exceptions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.TestRuns;

/// <summary>
/// Descarga una evidencia con ACL de proyecto + ownership Evidence↔TestRun (Sprint 11-C).
/// Preferir <paramref name="EvidenceId"/>; <paramref name="Path"/> solo si coincide con
/// <see cref="Evidence.FilePath"/> de ese run (legacy).
/// </summary>
public sealed record DownloadEvidenceQuery(
    Guid TestRunId,
    Guid? EvidenceId = null,
    string? Path = null) : IRequest<EvidenceStreamResult>;

public sealed record EvidenceStreamResult(Stream Content, string ContentType, string FileName);

public sealed class DownloadEvidenceQueryHandler(
    ITestRunRepository testRunRepo,
    IEvidenceStorage storage,
    IProjectAccessService access,
    ICurrentUserService currentUser)
    : IRequestHandler<DownloadEvidenceQuery, EvidenceStreamResult>
{
    private static readonly string[] RolesWithEvidenceAccess =
        [SystemRoles.Administrator, SystemRoles.QA, SystemRoles.TechLead];

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".webm", ".mp4", ".json", ".html", ".log", ".txt" };

    public async Task<EvidenceStreamResult> Handle(DownloadEvidenceQuery request, CancellationToken ct)
    {
        await access.EnsureCanAccessTestRunAsync(request.TestRunId, ct);

        if (!RolesWithEvidenceAccess.Any(currentUser.IsInRole))
            throw new ForbiddenAccessException("No tiene permiso para descargar evidencias de este proyecto.");

        var run = await testRunRepo.GetWithResultsAsync(request.TestRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.TestRunId);

        var evidence = ResolveEvidence(run, request.EvidenceId, request.Path)
            ?? throw new NotFoundException(nameof(Evidence), request.EvidenceId ?? (object)(request.Path ?? "unknown"));

        if (!IsValidEvidencePath(evidence.FilePath))
            throw new DomainException("Ruta de evidencia inválida.");

        var stream = await storage.OpenReadAsync(evidence.FilePath, ct);
        var contentType = ResolveContentType(evidence.ContentType, evidence.FilePath);
        return new EvidenceStreamResult(stream, contentType, Path.GetFileName(evidence.FilePath));
    }

    private static Evidence? ResolveEvidence(TestRun run, Guid? evidenceId, string? path)
    {
        var all = run.Results.SelectMany(r => r.Evidences).ToList();

        if (evidenceId is Guid id)
            return all.FirstOrDefault(e => e.Id == id);

        if (!string.IsNullOrWhiteSpace(path))
        {
            var normalized = path.Replace('\\', '/').Trim();
            return all.FirstOrDefault(e =>
                string.Equals(e.FilePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static string ResolveContentType(string stored, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webm" => "video/webm",
            ".mp4" => "video/mp4",
            ".json" => "application/json",
            ".html" => "text/html",
            ".log" or ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static bool IsValidEvidencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("..") || Path.IsPathRooted(normalized))
            return false;

        return AllowedExtensions.Contains(Path.GetExtension(path));
    }
}
