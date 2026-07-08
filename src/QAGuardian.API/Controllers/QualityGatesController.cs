using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.QualityGates;

namespace QAGuardian.API.Controllers;

[Authorize]
public class QualityGatesController : ApiControllerBase
{
    /// <summary>Lista los quality gates configurados.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await Mediator.Send(new GetQualityGatesQuery(), ct));

    /// <summary>Crea un quality gate con sus condiciones.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Create([FromBody] CreateQualityGateCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Asigna un quality gate a un proyecto.</summary>
    [HttpPost("assign")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Assign([FromBody] AssignGateToProjectCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
