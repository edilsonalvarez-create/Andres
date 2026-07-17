using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Integrations;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class PostmanImportHandlerTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ITestCaseRepository _testCases = Substitute.For<ITestCaseRepository>();
    private readonly IRepository<IntegrationSetting> _settings = Substitute.For<IRepository<IntegrationSetting>>();
    private readonly IEvidenceStorage _storage = Substitute.For<IEvidenceStorage>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private ImportPostmanCollectionCommandHandler CreateHandler()
        => new(_projects, _testCases, _settings, _storage, _access, _uow);

    private const string ValidCollection = """
        {
          "info": { "name": "API de Pagos", "schema": "https://schema.getpostman.com/json/collection/v2.1.0/" },
          "variable": [ { "key": "baseUrl", "value": "https://api" } ],
          "item": [
            { "name": "Crear pago", "request": { "method": "POST", "url": "{{baseUrl}}/pagos" } },
            { "name": "Carpeta", "item": [
              { "name": "Consultar pago", "request": { "method": "GET", "url": "{{baseUrl}}/pagos/1" } }
            ] }
          ]
        }
        """;

    public PostmanImportHandlerTests()
    {
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Project("PAY", "Pagos", null, null));
        _testCases.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<TestCase, bool>>>(),
            Arg.Any<CancellationToken>()).Returns(0);
        _storage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(0));
        _storage.GetAbsolutePath(Arg.Any<string>()).Returns(ci => "/abs/" + ci.ArgAt<string>(0));
        // El repositorio real devuelve lista vacía (no null) cuando no hay integración previa.
        _settings.ListAsync(
            Arg.Any<System.Linq.Expressions.Expression<Func<IntegrationSetting, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<IntegrationSetting>());
    }

    [Fact]
    public async Task Importa_collection_valida_y_registra_caso_de_prueba_api()
    {
        var result = await CreateHandler().Handle(
            new ImportPostmanCollectionCommand(Guid.NewGuid(), ValidCollection, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CollectionName.Should().Be("API de Pagos");
        result.Value.RequestsImported.Should().Be(2, "cuenta requests de forma recursiva");
        result.Value.VariablesImported.Should().Be(1);
        result.Value.TestCaseCode.Should().StartWith("TC-PM-");
        await _testCases.Received(1).AddAsync(
            Arg.Is<TestCase>(tc => tc.Type == TestType.Api && tc.Framework == AutomationFramework.Postman),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rechaza_json_que_no_es_collection_de_postman()
    {
        var result = await CreateHandler().Handle(
            new ImportPostmanCollectionCommand(Guid.NewGuid(), "{\"foo\":1}", null, null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("collection de Postman válida");
    }

    [Fact]
    public async Task Rechaza_json_malformado()
    {
        var result = await CreateHandler().Handle(
            new ImportPostmanCollectionCommand(Guid.NewGuid(), "{ no es json", null, null), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Con_environment_registra_variables_y_lo_almacena()
    {
        const string env = """
            { "name": "QA", "values": [ { "key": "token", "value": "abc" }, { "key": "user", "value": "x" } ] }
            """;

        var result = await CreateHandler().Handle(
            new ImportPostmanCollectionCommand(Guid.NewGuid(), ValidCollection, env, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnvironmentImported.Should().BeTrue();
        result.Value.VariablesImported.Should().Be(3, "1 de la collection + 2 del environment");
    }
}
