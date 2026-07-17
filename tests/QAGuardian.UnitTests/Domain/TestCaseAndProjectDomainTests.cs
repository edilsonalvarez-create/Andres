using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class TestCaseAndProjectDomainTests
{
    private static TestCase NewCase() =>
        new(Guid.NewGuid(), "tc-1", "Login", TestType.Functional, TestPriority.High);

    // ── TestCase ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_normaliza_el_codigo_a_mayusculas()
    {
        NewCase().Code.Should().Be("TC-1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rechaza_codigo_vacio(string code)
    {
        var act = () => new TestCase(Guid.NewGuid(), code, "T", TestType.Functional, TestPriority.Low);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddStep_rechaza_orden_duplicado()
    {
        var tc = NewCase();
        tc.AddStep(1, "a", "ok");
        var act = () => tc.AddStep(1, "b", "ok");
        act.Should().Throw<DomainException>().WithMessage("*orden 1*");
    }

    [Fact]
    public void Automate_con_framework_Manual_falla()
    {
        var act = () => NewCase().Automate(AutomationFramework.Manual, "x");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Automate_con_ruta_vacia_falla()
    {
        var act = () => NewCase().Automate(AutomationFramework.Playwright, "  ");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Automate_valido_fija_framework_y_ruta_recortada()
    {
        var tc = NewCase();
        tc.Automate(AutomationFramework.Postman, "  collection.json  ");
        tc.Framework.Should().Be(AutomationFramework.Postman);
        tc.AutomationScriptPath.Should().Be("collection.json");
    }

    [Fact]
    public void ClearSteps_vacia_la_coleccion()
    {
        var tc = NewCase();
        tc.AddStep(1, "a", "ok");
        tc.AddStep(2, "b", "ok");
        tc.ClearSteps();
        tc.Steps.Should().BeEmpty();
    }

    // ── Project ────────────────────────────────────────────────────────

    [Fact]
    public void SetCode_rechaza_mas_de_20_caracteres()
    {
        var act = () => new Project(new string('A', 21), "Nombre", null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddModule_rechaza_nombre_duplicado_ignorando_mayusculas()
    {
        var p = new Project("ERP", "Sistema", null, null);
        p.AddModule("Autenticación", null);
        var act = () => p.AddModule("AUTENTICACIÓN", null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddVersion_rechaza_numero_duplicado()
    {
        var p = new Project("ERP", "Sistema", null, null);
        p.AddVersion("1.0.0", null);
        var act = () => p.AddVersion("1.0.0", "otra");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignQualityGate_guarda_el_id()
    {
        var p = new Project("ERP", "Sistema", null, null);
        var gateId = Guid.NewGuid();
        p.AssignQualityGate(gateId);
        p.QualityGateId.Should().Be(gateId);
    }

    [Fact]
    public void Deactivate_y_Activate_alternan_el_estado()
    {
        var p = new Project("ERP", "Sistema", null, null);
        p.Deactivate();
        p.IsActive.Should().BeFalse();
        p.Activate();
        p.IsActive.Should().BeTrue();
    }
}
