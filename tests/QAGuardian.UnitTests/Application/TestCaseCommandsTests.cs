using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.TestCases;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class TestCaseCommandsTests
{
    private readonly ITestCaseRepository _testCases = Substitute.For<ITestCaseRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public TestCaseCommandsTests()
    {
        _access.CanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Crear_persiste_los_pasos_ordenados()
    {
        var projectId = Guid.NewGuid();
        _projects.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project("ERP", "Sistema", null, null));
        _testCases.AnyAsync(Arg.Any<Expression<Func<TestCase, bool>>>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateTestCaseCommandHandler(_testCases, _projects, _access, _uow);
        var result = await handler.Handle(new CreateTestCaseCommand(
            projectId, "TC-1", "Login válido", TestType.Functional, TestPriority.High, null, null, null, null,
            [new TestStepDto(2, "Enviar", "OK"), new TestStepDto(1, "Ingresar credenciales", "form listo")]), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Steps.Should().HaveCount(2);
        result.Value.Steps[0].Order.Should().Be(1);
        await _testCases.Received(1).AddAsync(Arg.Any<TestCase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_en_proyecto_inexistente_lanza_NotFound()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Project?)null);
        var handler = new CreateTestCaseCommandHandler(_testCases, _projects, _access, _uow);

        var act = () => handler.Handle(new CreateTestCaseCommand(
            Guid.NewGuid(), "TC-1", "T", TestType.Functional, TestPriority.Low, null, null, null, null, []), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Crear_en_proyecto_ajeno_lanza_NotFound()
    {
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new NotFoundException(nameof(Project), Guid.NewGuid()));

        var handler = new CreateTestCaseCommandHandler(_testCases, _projects, _access, _uow);
        var act = () => handler.Handle(new CreateTestCaseCommand(
            Guid.NewGuid(), "TC-1", "T", TestType.Functional, TestPriority.Low, null, null, null, null, []), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Crear_rechaza_codigo_duplicado_en_el_proyecto()
    {
        var projectId = Guid.NewGuid();
        _projects.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project("ERP", "Sistema", null, null));
        _testCases.AnyAsync(Arg.Any<Expression<Func<TestCase, bool>>>(), Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateTestCaseCommandHandler(_testCases, _projects, _access, _uow);
        var result = await handler.Handle(new CreateTestCaseCommand(
            projectId, "TC-1", "T", TestType.Functional, TestPriority.Low, null, null, null, null, []), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("TC-1");
        await _testCases.DidNotReceive().AddAsync(Arg.Any<TestCase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Automatizar_vincula_framework_script_y_activa_el_caso()
    {
        var tc = new TestCase(Guid.NewGuid(), "TC-1", "Login", TestType.Functional, TestPriority.High, null, null, null);
        tc.Status.Should().Be(TestCaseStatus.Draft);
        _testCases.GetWithStepsAsync(tc.Id, Arg.Any<CancellationToken>()).Returns(tc);

        var handler = new AutomateTestCaseCommandHandler(_testCases, _access, _uow);
        var result = await handler.Handle(
            new AutomateTestCaseCommand(tc.Id, AutomationFramework.Playwright, "specs/login.spec.ts"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Framework.Should().Be(AutomationFramework.Playwright);
        result.Value.AutomationScriptPath.Should().Be("specs/login.spec.ts");
        result.Value.Status.Should().Be(TestCaseStatus.Active);
    }

    [Fact]
    public async Task GetById_caso_de_proyecto_ajeno_lanza_NotFound()
    {
        var tc = new TestCase(Guid.NewGuid(), "TC-1", "Login", TestType.Functional, TestPriority.High, null, null, null);
        _testCases.GetWithStepsAsync(tc.Id, Arg.Any<CancellationToken>()).Returns(tc);
        _access.CanAccessProjectAsync(tc.ProjectId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetTestCaseByIdQueryHandler(_testCases, _access);
        var act = () => handler.Handle(new GetTestCaseByIdQuery(tc.Id), default);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*TestCase*");
    }

    [Fact]
    public async Task Actualizar_reemplaza_todos_los_pasos()
    {
        var tc = new TestCase(Guid.NewGuid(), "TC-1", "Original", TestType.Functional, TestPriority.Low, null, null, null);
        tc.AddStep(1, "viejo paso", "viejo");
        _testCases.GetWithStepsAsync(tc.Id, Arg.Any<CancellationToken>()).Returns(tc);

        var handler = new UpdateTestCaseCommandHandler(_testCases, _access, _uow);
        var result = await handler.Handle(new UpdateTestCaseCommand(
            tc.Id, "Actualizado", TestType.Regression, TestPriority.High, null, null,
            [new TestStepDto(1, "nuevo paso", "nuevo")]), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Actualizado");
        result.Value.Steps.Should().ContainSingle(s => s.Action == "nuevo paso");
    }

    [Fact]
    public async Task Listar_filtra_por_tipo()
    {
        var projectId = Guid.NewGuid();
        _testCases.PagedAsync(1, 20, Arg.Any<Expression<Func<TestCase, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(([new TestCase(projectId, "TC-1", "API test", TestType.Api, TestPriority.Low, null, null, null)], 1));

        var handler = new GetTestCasesQueryHandler(_testCases, _access);
        var result = await handler.Handle(new GetTestCasesQuery(projectId, Type: TestType.Api), default);

        result.Items.Single().Type.Should().Be(TestType.Api);
    }
}
