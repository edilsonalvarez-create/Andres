using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.TestCases;
using QAGuardian.Domain.Enums;

namespace QAGuardian.API.Controllers;

[Authorize]
public class TestCasesController : ApiControllerBase
{
    /// <summary>Lista casos de prueba de un proyecto.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetAll([FromQuery] Guid projectId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, [FromQuery] TestType? type = null,
        [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetTestCasesQuery(projectId, page, pageSize, type, search), ct));

    /// <summary>Obtiene un caso de prueba con sus pasos.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetTestCaseByIdQuery(id), ct));

    /// <summary>Crea un caso de prueba con sus pasos.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Create([FromBody] CreateTestCaseCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Actualiza un caso de prueba.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTestCaseCommand command, CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));

    /// <summary>Vincula un script de automatización al caso de prueba.</summary>
    [HttpPost("{id:guid}/automate")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Automate(Guid id, [FromBody] AutomateTestCaseCommand command, CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));

    /// <summary>Elimina (lógicamente) un caso de prueba.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => FromResult(await Mediator.Send(new DeleteTestCaseCommand(id), ct));

    /// <summary>Vincula (o desvincula) el caso a una historia de usuario para trazabilidad.</summary>
    [HttpPut("{id:guid}/user-story")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> LinkUserStory(Guid id, [FromBody] LinkTestCaseToUserStoryCommand command,
        CancellationToken ct)
        => id != command.Id
            ? BadRequest(new { error = "El identificador de la ruta no coincide con el cuerpo." })
            : FromResult(await Mediator.Send(command, ct));

    /// <summary>Crea o actualiza el script de un caso de prueba automatizado (autoría/edición).</summary>
    [HttpPost("script")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> SaveScript([FromBody] SaveTestScriptCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Obtiene el contenido del script de un caso de prueba para editarlo.</summary>
    [HttpGet("{id:guid}/script")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> GetScript(Guid id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetTestScriptQuery(id), ct));

    /// <summary>Ejecuta un único caso de prueba con el runner de su framework ("probar" desde el editor).</summary>
    [HttpPost("{id:guid}/run")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Run(Guid id, [FromQuery] EnvironmentType environment = EnvironmentType.QA,
        CancellationToken ct = default)
        => FromResult(await Mediator.Send(new RunTestCaseCommand(id, environment), ct));

    /// <summary>Graba una spec de Playwright con codegen (o genera un andamiaje si no hay entorno gráfico).</summary>
    [HttpPost("record")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Record([FromBody] RecordPlaywrightScriptCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
