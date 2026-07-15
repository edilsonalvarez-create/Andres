using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Exceptions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.TestRuns;

/// <summary>Descarga una evidencia validando ownership del test run y previniendo path traversal.</summary>
public sealed record DownloadEvidenceQuery(Guid TestRunId, string Path) : IRequest<EvidenceStreamResult>;

public sealed record EvidenceStreamResult(Stream Content, string ContentType, string FileName);

public sealed class DownloadEvidenceQueryHandler(
    ITestRunRepository testRunRepo,
    IEvidenceStorage storage,
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
        var testRun = await testRunRepo.GetByIdAsync(request.TestRunId, ct)
            ?? throw new NotFoundException(nameof(TestRun), request.TestRunId);

        if (!RolesWithEvidenceAccess.Any(currentUser.IsInRole))
            throw new ForbiddenAccessException("No tiene permiso para descargar evidencias de este proyecto.");

        if (!IsValidEvidencePath(request.Path))
            throw new DomainException("Ruta de evidencia inválida.");

        var stream = await storage.OpenReadAsync(request.Path, ct);
        var contentType = System.IO.Path.GetExtension(request.Path).ToLowerInvariant() switch
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

        return new EvidenceStreamResult(stream, contentType, System.IO.Path.GetFileName(request.Path));
    }

    private static bool IsValidEvidencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("..") || System.IO.Path.IsPathRooted(normalized))
            return false;

        return AllowedExtensions.Contains(System.IO.Path.GetExtension(path));
    }
}
