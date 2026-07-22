using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Runners;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// Regresión Sprint 4 (ADR-009): <see cref="TestRunnerFactory"/> resuelve indexando
/// <see cref="ITestRunner.Framework"/> de las instancias inyectadas, sin un <c>switch</c>
/// que haya que editar por cada runner nuevo. Estas pruebas fijan ese contrato.
/// </summary>
public class TestRunnerFactoryTests
{
    private static ITestRunner FakeRunner(AutomationFramework framework)
    {
        var runner = Substitute.For<ITestRunner>();
        runner.Framework.Returns(framework);
        return runner;
    }

    [Fact]
    public void Resuelve_el_runner_cuyo_Framework_coincide()
    {
        var playwright = FakeRunner(AutomationFramework.Playwright);
        var jmeter = FakeRunner(AutomationFramework.JMeter);
        var factory = new TestRunnerFactory([playwright, jmeter]);

        factory.Resolve(AutomationFramework.JMeter).Should().BeSameAs(jmeter);
        factory.Resolve(AutomationFramework.Playwright).Should().BeSameAs(playwright);
    }

    [Fact]
    public void Framework_sin_runner_registrado_lanza_NotSupportedException()
    {
        var factory = new TestRunnerFactory([FakeRunner(AutomationFramework.Playwright)]);

        var act = () => factory.Resolve(AutomationFramework.OwaspZap);

        act.Should().Throw<NotSupportedException>().WithMessage("*OwaspZap*");
    }

    [Theory]
    [InlineData(TestType.Api, AutomationFramework.Postman)]
    [InlineData(TestType.Performance, AutomationFramework.JMeter)]
    [InlineData(TestType.Security, AutomationFramework.OwaspZap)]
    [InlineData(TestType.Visual, AutomationFramework.VisualRegression)]
    [InlineData(TestType.Functional, AutomationFramework.Playwright)]
    [InlineData(TestType.Regression, AutomationFramework.Playwright)]
    [InlineData(TestType.Smoke, AutomationFramework.Playwright)]
    public void ResolveByTestType_mapea_al_framework_esperado(TestType testType, AutomationFramework expected)
    {
        var runners = Enum.GetValues<AutomationFramework>()
            .Where(f => f != AutomationFramework.Manual && f != AutomationFramework.SqlValidator)
            .Select(FakeRunner)
            .ToList();
        var factory = new TestRunnerFactory(runners);

        factory.ResolveByTestType(testType).Framework.Should().Be(expected);
    }

    [Fact]
    public void Agregar_un_runner_nuevo_no_requiere_tocar_la_fabrica()
    {
        // Documenta el fix de OCP (ADR-009): un framework fuera del enum original de pruebas
        // (aquí simulado con uno ya existente que antes no tenía runner, SqlValidator) se
        // resuelve con solo agregarlo a la colección inyectada — cero cambios en esta clase.
        var factory = new TestRunnerFactory([FakeRunner(AutomationFramework.SqlValidator)]);

        factory.Resolve(AutomationFramework.SqlValidator).Framework.Should().Be(AutomationFramework.SqlValidator);
    }
}
