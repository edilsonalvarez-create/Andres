using System.Text;
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

public class TestScriptHandlerTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly ITestCaseRepository _testCases = Substitute.For<ITestCaseRepository>();
    private readonly IEvidenceStorage _storage = Substitute.For<IEvidenceStorage>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public TestScriptHandlerTests()
    {
        _access.CanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Project("WEB", "Portal Web", null, null));
        _storage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(0));
        _storage.GetAbsolutePath(Arg.Any<string>()).Returns(ci => "/evidence/" + ci.ArgAt<string>(0));
        _testCases.CountAsync(Arg.Any<System.Linq.Expressions.Expression<Func<TestCase, bool>>>(),
            Arg.Any<CancellationToken>()).Returns(0);
    }

    [Fact]
    public async Task Guardar_script_nuevo_crea_caso_de_prueba_automatizado()
    {
        var handler = new SaveTestScriptCommandHandler(_projects, _testCases, _storage, _access, _uow);
        var spec = "import { test } from '@playwright/test';\ntest('login', async ({ page }) => {});";

        var result = await handler.Handle(new SaveTestScriptCommand(
            Guid.NewGuid(), null, "Login válido", AutomationFramework.Playwright, TestType.Functional, spec), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().StartWith("TC-SC-");
        result.Value.Framework.Should().Be(AutomationFramework.Playwright);
        result.Value.ScriptPath.Should().EndWith(".spec.ts");
        await _testCases.Received(1).AddAsync(
            Arg.Is<TestCase>(tc => tc.Framework == AutomationFramework.Playwright && tc.Status == TestCaseStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Guardar_script_existente_actualiza_el_caso()
    {
        var existing = new TestCase(Guid.NewGuid(), "TC-0001", "Login", TestType.Functional, TestPriority.High);
        _testCases.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var handler = new SaveTestScriptCommandHandler(_projects, _testCases, _storage, _access, _uow);

        var result = await handler.Handle(new SaveTestScriptCommand(
            existing.ProjectId, existing.Id, "Login actualizado", AutomationFramework.Playwright,
            TestType.Regression, "test('x', async () => {});"), default);

        result.IsSuccess.Should().BeTrue();
        existing.Framework.Should().Be(AutomationFramework.Playwright);
        existing.AutomationScriptPath.Should().NotBeNull();
        await _testCases.DidNotReceive().AddAsync(Arg.Any<TestCase>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leer_script_devuelve_el_contenido_almacenado()
    {
        var testCase = new TestCase(Guid.NewGuid(), "TC-0001", "Login", TestType.Functional, TestPriority.High);
        testCase.Automate(AutomationFramework.Playwright, "scripts/x/login.spec.ts");
        _testCases.GetByIdAsync(testCase.Id, Arg.Any<CancellationToken>()).Returns(testCase);
        _storage.OpenReadAsync("scripts/x/login.spec.ts", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("contenido de la spec")));

        var handler = new GetTestScriptQueryHandler(_testCases, _storage, _access);
        var dto = await handler.Handle(new GetTestScriptQuery(testCase.Id), default);

        dto.Content.Should().Be("contenido de la spec");
        dto.Framework.Should().Be(AutomationFramework.Playwright);
    }

    [Fact]
    public async Task Leer_script_de_caso_sin_automatizar_falla()
    {
        var testCase = new TestCase(Guid.NewGuid(), "TC-0001", "Manual", TestType.Functional, TestPriority.Medium);
        _testCases.GetByIdAsync(testCase.Id, Arg.Any<CancellationToken>()).Returns(testCase);
        var handler = new GetTestScriptQueryHandler(_testCases, _storage, _access);

        var act = () => handler.Handle(new GetTestScriptQuery(testCase.Id), default);
        await act.Should().ThrowAsync<DomainException>();
    }
}
