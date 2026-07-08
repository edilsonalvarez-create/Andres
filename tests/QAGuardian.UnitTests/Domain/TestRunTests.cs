using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class TestRunTests
{
    private static TestRun NewRun() =>
        new(Guid.NewGuid(), TestType.Regression, EnvironmentType.QA, "qa@test.com");

    [Fact]
    public void Start_deja_la_ejecucion_en_estado_Running()
    {
        var run = NewRun();
        run.Start();
        run.Status.Should().Be(RunStatus.Running);
        run.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_falla_si_la_ejecucion_no_esta_pendiente()
    {
        var run = NewRun();
        run.Start();
        var act = () => run.Start();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddResult_falla_si_la_ejecucion_no_esta_en_curso()
    {
        var run = NewRun();
        var act = () => run.AddResult("prueba", ResultStatus.Passed, 100);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PassRate_se_calcula_sobre_los_resultados()
    {
        var run = NewRun();
        run.Start();
        run.AddResult("a", ResultStatus.Passed, 10);
        run.AddResult("b", ResultStatus.Passed, 10);
        run.AddResult("c", ResultStatus.Failed, 10);
        run.AddResult("d", ResultStatus.Skipped, 10);

        run.TotalTests.Should().Be(4);
        run.Passed.Should().Be(2);
        run.Failed.Should().Be(1);
        run.Skipped.Should().Be(1);
        run.PassRatePercent.Should().Be(50m);
    }

    [Fact]
    public void Complete_cierra_la_ejecucion_y_registra_fecha()
    {
        var run = NewRun();
        run.Start();
        run.Complete();
        run.Status.Should().Be(RunStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_falla_sobre_una_ejecucion_finalizada()
    {
        var run = NewRun();
        run.Start();
        run.Complete();
        var act = () => run.Cancel();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AttachEvidence_agrega_evidencia_al_resultado()
    {
        var run = NewRun();
        run.Start();
        var result = run.AddResult("con evidencia", ResultStatus.Failed, 50,
            errorMessage: "assert falló");
        result.AttachEvidence(EvidenceType.Screenshot, "runs/x/captura.png", "image/png", 2048);

        result.Evidences.Should().ContainSingle()
            .Which.Type.Should().Be(EvidenceType.Screenshot);
    }
}
