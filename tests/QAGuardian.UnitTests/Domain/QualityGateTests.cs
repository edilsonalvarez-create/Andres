using FluentAssertions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Domain;

public class QualityGateTests
{
    [Fact]
    public void Evaluate_aprueba_cuando_todas_las_condiciones_se_cumplen()
    {
        var gate = new QualityGate("Gate estricto");
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        gate.AddCondition(GateMetric.CriticalVulnerabilities, GateOperator.Equal, 0m);

        var evaluation = gate.Evaluate(Guid.NewGuid(), new Dictionary<GateMetric, decimal>
        {
            [GateMetric.PassRatePercent] = 100m,
            [GateMetric.CriticalVulnerabilities] = 0m
        });

        evaluation.Status.Should().Be(QualityGateStatus.Passed);
        evaluation.DeploymentApproved.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_rechaza_el_despliegue_cuando_falla_una_condicion_bloqueante()
    {
        var gate = new QualityGate("Gate estricto");
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);

        var evaluation = gate.Evaluate(Guid.NewGuid(), new Dictionary<GateMetric, decimal>
        {
            [GateMetric.PassRatePercent] = 80m
        });

        evaluation.Status.Should().Be(QualityGateStatus.Failed);
        evaluation.DeploymentApproved.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_genera_advertencia_cuando_falla_una_condicion_no_bloqueante()
    {
        var gate = new QualityGate("Gate flexible");
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        gate.AddCondition(GateMetric.HighVulnerabilities, GateOperator.LessOrEqual, 0m, isBlocking: false);

        var evaluation = gate.Evaluate(Guid.NewGuid(), new Dictionary<GateMetric, decimal>
        {
            [GateMetric.PassRatePercent] = 99m,
            [GateMetric.HighVulnerabilities] = 3m
        });

        evaluation.Status.Should().Be(QualityGateStatus.Warning);
        evaluation.DeploymentApproved.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_trata_las_metricas_ausentes_como_incumplidas()
    {
        var gate = new QualityGate("Gate con cobertura");
        gate.AddCondition(GateMetric.CoveragePercent, GateOperator.GreaterOrEqual, 80m);

        var evaluation = gate.Evaluate(Guid.NewGuid(), new Dictionary<GateMetric, decimal>());

        evaluation.Status.Should().Be(QualityGateStatus.Failed);
    }

    [Fact]
    public void AddCondition_no_permite_metricas_duplicadas()
    {
        var gate = new QualityGate("Gate");
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 90m);
        var act = () => gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GetConditionResults_se_reconstruye_desde_el_json_persistido()
    {
        var gate = new QualityGate("Gate");
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        var evaluation = gate.Evaluate(Guid.NewGuid(), new Dictionary<GateMetric, decimal>
        {
            [GateMetric.PassRatePercent] = 97m
        });

        evaluation.GetConditionResults().Should().ContainSingle()
            .Which.Satisfied.Should().BeTrue();
    }
}
