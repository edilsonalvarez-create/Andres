using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Solicitud de aprobación humana (activación de caso, release de versión, override de gate).</summary>
public class ApprovalRequest : AuditableEntity
{
    private ApprovalRequest() { }

    public ApprovalRequest(
        Guid projectId,
        ApprovalType type,
        Guid targetEntityId,
        string title,
        Guid requestedByUserId,
        string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("El título de la aprobación es obligatorio.");
        ProjectId = projectId;
        Type = type;
        TargetEntityId = targetEntityId;
        Title = title.Trim();
        RequestedByUserId = requestedByUserId;
        Comment = comment;
        Status = ApprovalStatus.Pending;
    }

    public Guid ProjectId { get; private set; }
    public ApprovalType Type { get; private set; }
    public Guid TargetEntityId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Comment { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionComment { get; private set; }

    public void Approve(Guid decidedByUserId, string? decisionComment = null)
    {
        EnsurePending();
        Status = ApprovalStatus.Approved;
        DecidedByUserId = decidedByUserId;
        DecidedAt = DateTime.UtcNow;
        DecisionComment = decisionComment;
    }

    public void Reject(Guid decidedByUserId, string? decisionComment = null)
    {
        EnsurePending();
        Status = ApprovalStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAt = DateTime.UtcNow;
        DecisionComment = decisionComment;
    }

    private void EnsurePending()
    {
        if (Status != ApprovalStatus.Pending)
            throw new DomainException("La solicitud de aprobación ya fue resuelta.");
    }
}
