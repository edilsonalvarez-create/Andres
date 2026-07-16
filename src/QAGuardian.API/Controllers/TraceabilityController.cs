using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.Traceability;

namespace QAGuardian.API.Controllers;

/// <summary>Trazabilidad enterprise: cobertura Req → Historia → Caso → Resultado → Defecto.</summary>
[Authorize]
public class TraceabilityController : ApiControllerBase
{
    [HttpGet("projects/{projectId:guid}/traceability/coverage")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetCoverage(Guid projectId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetRequirementsCoverageQuery(projectId), ct));
}
