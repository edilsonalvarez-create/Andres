using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Ejecución de pipeline CI/CD vinculada al quality gate.</summary>
public class PipelineExecution : AuditableEntity
{
    private PipelineExecution() { } // EF Core

    public PipelineExecution(Guid projectId, PipelineProvider provider, string externalRunId,
        string branch, string commitSha, string? url)
    {
        ProjectId = projectId;
        Provider = provider;
        ExternalRunId = externalRunId;
        Branch = branch;
        CommitSha = commitSha;
        Url = url;
        Status = RunStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public Guid ProjectId { get; private set; }
    public PipelineProvider Provider { get; private set; }
    public string ExternalRunId { get; private set; } = default!;
    public string Branch { get; private set; } = default!;
    public string CommitSha { get; private set; } = default!;
    public string? Url { get; private set; }
    public RunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool? DeploymentApproved { get; private set; }

    public void Complete(bool deploymentApproved)
    {
        Status = RunStatus.Completed;
        FinishedAt = DateTime.UtcNow;
        DeploymentApproved = deploymentApproved;
    }

    public void Fail()
    {
        Status = RunStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        DeploymentApproved = false;
    }
}
