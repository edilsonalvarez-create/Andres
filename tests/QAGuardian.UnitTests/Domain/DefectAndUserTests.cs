using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class DefectTests
{
    private static Defect NewDefect() => new(
        Guid.NewGuid(), "BUG-00001", "Fallo en login", "Descripción",
        DefectSeverity.Major, DefectPriority.High, Guid.NewGuid());

    [Fact]
    public void Flujo_completo_del_defecto_respeta_las_transiciones()
    {
        var defect = NewDefect();
        defect.Status.Should().Be(DefectStatus.New);

        defect.Assign(Guid.NewGuid());
        defect.Status.Should().Be(DefectStatus.Assigned);

        defect.StartProgress();
        defect.Status.Should().Be(DefectStatus.InProgress);

        defect.Resolve();
        defect.Status.Should().Be(DefectStatus.Resolved);
        defect.ResolvedAt.Should().NotBeNull();

        defect.Verify();
        defect.Close();
        defect.Status.Should().Be(DefectStatus.Closed);
        defect.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void No_se_puede_verificar_un_defecto_que_no_esta_resuelto()
    {
        var defect = NewDefect();
        var act = () => defect.Verify();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reopen_limpia_las_fechas_de_resolucion()
    {
        var defect = NewDefect();
        defect.Assign(Guid.NewGuid());
        defect.Resolve();
        defect.Reopen();

        defect.Status.Should().Be(DefectStatus.Reopened);
        defect.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void No_se_puede_asignar_un_defecto_cerrado()
    {
        var defect = NewDefect();
        defect.Assign(Guid.NewGuid());
        defect.Resolve();
        defect.Verify();
        defect.Close();

        var act = () => defect.Assign(Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }
}

public class UserTests
{
    [Fact]
    public void Cinco_intentos_fallidos_bloquean_la_cuenta_temporalmente()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        for (var i = 0; i < 5; i++) user.RegisterFailedLogin();

        user.IsLocked.Should().BeTrue();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Login_exitoso_reinicia_el_contador_de_intentos()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        user.RegisterFailedLogin();
        user.RegisterSuccessfulLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.IsLocked.Should().BeFalse();
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public void Cambiar_contrasena_revoca_los_refresh_tokens_activos()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        var token = user.AddRefreshToken("token-1", DateTime.UtcNow.AddDays(7));
        token.IsActive.Should().BeTrue();

        user.ChangePassword("nuevo-hash");
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Correo_invalido_es_rechazado()
    {
        var act = () => new User("sin-arroba", "Nombre", "hash");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AssignRole_es_idempotente()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        var role = new Role(SystemRoles.QA, "QA");
        user.AssignRole(role);
        user.AssignRole(role);
        user.Roles.Should().HaveCount(1);
    }
}

public class ProjectTests
{
    [Fact]
    public void No_permite_modulos_duplicados()
    {
        var project = new Project("ERP", "Sistema ERP", null, null);
        project.AddModule("Facturación", null);
        var act = () => project.AddModule("facturación", null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void El_codigo_se_normaliza_a_mayusculas()
    {
        var project = new Project("erp-01", "Sistema ERP", null, null);
        project.Code.Should().Be("ERP-01");
    }

    [Fact]
    public void TestCase_no_permite_pasos_con_orden_duplicado()
    {
        var testCase = new TestCase(Guid.NewGuid(), "TC-001", "Login válido",
            TestType.Functional, TestPriority.High);
        testCase.AddStep(1, "Abrir la página de login", "Se muestra el formulario");
        var act = () => testCase.AddStep(1, "Otra acción", "Otro resultado");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Automate_exige_framework_distinto_de_manual()
    {
        var testCase = new TestCase(Guid.NewGuid(), "TC-001", "Login válido",
            TestType.Functional, TestPriority.High);
        var act = () => testCase.Automate(AutomationFramework.Manual, "spec.ts");
        act.Should().Throw<DomainException>();
    }
}
