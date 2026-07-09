using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Features.Integrations;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class GeneratePipelineHandlerTests
{
    [Fact]
    public async Task Genera_yaml_con_el_flujo_completo_y_los_datos_del_proyecto()
    {
        var projects = Substitute.For<IProjectRepository>();
        var project = new Project("ERP", "Sistema ERP", null, null);
        projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(project);

        var result = await new GenerateGitHubActionsPipelineQueryHandler(projects)
            .Handle(new GenerateGitHubActionsPipelineQuery(project.Id), default);

        result.FileName.Should().Be("qa-guardian-erp.yml");
        // Flujo Build -> Unit -> (Playwright/API/Security/Performance) -> Deploy Staging -> Smoke -> Producción
        result.Yaml.Should().ContainAll(
            "build:", "unit-test:", "qa-guardian-gate:", "deploy-staging:",
            "smoke-test:", "deploy-production:");
        result.Yaml.Should().Contain("Sistema ERP").And.Contain(project.Id.ToString());
        // Preserva la sintaxis de GitHub Actions sin que C# la interprete.
        result.Yaml.Should().Contain("${{ secrets.QAGUARDIAN_URL }}");
        result.Yaml.Should().Contain("runType: [1, 3, 5, 4]");
    }
}
