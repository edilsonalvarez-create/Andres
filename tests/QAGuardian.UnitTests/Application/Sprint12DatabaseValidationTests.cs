using FluentAssertions;
using FluentValidation.TestHelper;
using QAGuardian.Application.Features.DatabaseValidation;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class Sprint12DatabaseValidationTests
{
    [Theory]
    [InlineData("SourceConnectionString")]
    [InlineData("targetConnectionString")]
    [InlineData("connectionString")]
    [InlineData("source_conn")]
    [InlineData("TargetConn")]
    public void StartRequest_rechaza_claves_de_connection_string(string key)
    {
        var request = new StartDatabaseValidationRequest
        {
            ProjectId = Guid.NewGuid(),
            SourceEnvironment = "dev",
            TargetEnvironment = "staging",
            ExtensionData = new Dictionary<string, System.Text.Json.JsonElement>
            {
                [key] = System.Text.Json.JsonSerializer.SerializeToElement(
                    "Server=evil;Password=x")
            }
        };

        var act = () => request.ToCommand();
        act.Should().Throw<QAGuardian.Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public void StartRequest_acepta_solo_nombres_de_entorno()
    {
        var projectId = Guid.NewGuid();
        var request = new StartDatabaseValidationRequest
        {
            ProjectId = projectId,
            SourceEnvironment = "dev",
            TargetEnvironment = "staging"
        };

        var command = request.ToCommand();
        command.ProjectId.Should().Be(projectId);
        command.SourceEnvironment.Should().Be("dev");
        command.TargetEnvironment.Should().Be("staging");
    }

    [Theory]
    [InlineData("Server=localhost;Database=x;User Id=sa;Password=secret")]
    [InlineData("Data Source=.;Initial Catalog=db;Password=x")]
    public void Validator_rechaza_environment_que_parece_connection_string(string fakeEnv)
    {
        var validator = new StartDatabaseValidationCommandValidator();
        var result = validator.TestValidate(new StartDatabaseValidationCommand(
            Guid.NewGuid(), fakeEnv, "staging"));
        result.ShouldHaveValidationErrorFor(x => x.SourceEnvironment);
    }

    [Fact]
    public void Validator_acepta_nombres_cortos()
    {
        var validator = new StartDatabaseValidationCommandValidator();
        var result = validator.TestValidate(new StartDatabaseValidationCommand(
            Guid.NewGuid(), "dev", "staging"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Hangfire_enqueue_firma_no_incluye_connection_strings()
    {
        var method = typeof(QAGuardian.Application.Abstractions.Services.IBackgroundJobScheduler)
            .GetMethod(nameof(QAGuardian.Application.Abstractions.Services.IBackgroundJobScheduler.EnqueueDatabaseValidation));
        method.Should().NotBeNull();
        var paramNames = method!.GetParameters().Select(p => p.Name!.ToLowerInvariant()).ToArray();
        paramNames.Should().Contain(new[] { "validationrunid", "projectid", "sourceenvironment", "targetenvironment" });
        paramNames.Should().NotContain(n => n.Contains("conn", StringComparison.Ordinal));
        paramNames.Should().NotContain(n => n.Contains("connection", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectDatabaseEnvironment_normaliza_nombre()
    {
        var env = new ProjectDatabaseEnvironment(Guid.NewGuid(), " Dev ", "cipher");
        env.Name.Should().Be("dev");
        env.IsActive.Should().BeTrue();
    }
}
