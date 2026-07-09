using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Features.DatabaseValidation;
using QAGuardian.Application.Features.Integrations;

namespace QAGuardian.API.Controllers;

[Authorize]
public class IntegrationsController : ApiControllerBase
{
    /// <summary>Configura o actualiza una integración externa del proyecto.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> Upsert([FromBody] UpsertIntegrationCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Métricas de SonarQube: cobertura, duplicación, bugs, hotspots, vulnerabilidades y code smells.</summary>
    [HttpGet("sonarqube/{projectId:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetSonarMetrics(Guid projectId, CancellationToken ct)
    {
        var metrics = await Mediator.Send(new GetSonarMetricsQuery(projectId), ct);
        return metrics is null
            ? NotFound(new { error = "SonarQube no está configurado para este proyecto." })
            : Ok(metrics);
    }

    /// <summary>Pull Requests del repositorio GitHub del proyecto.</summary>
    [HttpGet("github/{projectId:guid}/pulls")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetPullRequests(Guid projectId, [FromQuery] string state = "open",
        CancellationToken ct = default)
        => Ok(await Mediator.Send(new GetGitHubPullRequestsQuery(projectId, state), ct));

    /// <summary>
    /// Agente inteligente de PR: detecta archivos modificados, genera casos de prueba con IA,
    /// ejecuta smoke tests y comenta el resultado en el Pull Request.
    /// </summary>
    [HttpPost("github/{projectId:guid}/pulls/{prNumber:int}/analyze")]
    [Authorize(Policy = Policies.ExecuteTests)]
    public async Task<IActionResult> AnalyzePullRequest(Guid projectId, int prNumber, CancellationToken ct)
        => FromResult(await Mediator.Send(new AnalyzePullRequestCommand(projectId, prNumber), ct));

    /// <summary>Inicia una comparación de esquemas SQL Server entre dos ambientes.</summary>
    [HttpPost("database-validation")]
    [Authorize(Policy = Policies.ExecuteTests)]
    public async Task<IActionResult> StartDatabaseValidation(
        [FromBody] StartDatabaseValidationCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Historial de validaciones de base de datos de un proyecto.</summary>
    [HttpGet("database-validation/{projectId:guid}")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GetDatabaseValidations(Guid projectId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetDatabaseValidationsQuery(projectId), ct));

    /// <summary>
    /// Importa una collection de Postman (y opcionalmente su environment): valida, almacena
    /// y registra un caso de prueba automatizado de tipo API listo para ejecutarse con Newman.
    /// </summary>
    [HttpPost("postman/import")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<IActionResult> ImportPostman([FromBody] ImportPostmanCollectionCommand command,
        CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Genera automáticamente el pipeline de GitHub Actions (quality gate) del proyecto.</summary>
    [HttpGet("github/{projectId:guid}/pipeline")]
    [Authorize(Policy = Policies.ViewReports)]
    public async Task<IActionResult> GeneratePipeline(Guid projectId, [FromQuery] bool download = false,
        CancellationToken ct = default)
    {
        var pipeline = await Mediator.Send(new GenerateGitHubActionsPipelineQuery(projectId), ct);
        return download
            ? File(System.Text.Encoding.UTF8.GetBytes(pipeline.Yaml), "text/yaml", pipeline.FileName)
            : Ok(pipeline);
    }
}
