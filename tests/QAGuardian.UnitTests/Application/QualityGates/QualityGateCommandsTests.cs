using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Features.QualityGates;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application.QualityGates;

public class QualityGateCommandsTests
{
    private readonly IQualityGateRepository _gates = Substitute.For<IQualityGateRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Crea_el_gate_con_sus_condiciones()
    {
        _gates.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns((QualityGate?)null);

        var handler = new CreateQualityGateCommandHandler(_gates, _uow);
        var command = new CreateQualityGateCommand(
            "Production Gate",
            false,
            [
                new ConditionInput(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 90, true),
                new ConditionInput(GateMetric.CoveragePercent, GateOperator.GreaterOrEqual, 75, false),
            ]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Production Gate");
        result.Value.Conditions.Should().HaveCount(2);
        await _gates.Received(1).AddAsync(Arg.Any<QualityGate>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rechaza_nombre_vacio()
    {
        _gates.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns((QualityGate?)null);

        var handler = new CreateQualityGateCommandHandler(_gates, _uow);
        var command = new CreateQualityGateCommand(
            "",
            false,
            [new ConditionInput(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 90, true)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Actualiza_nombre_y_reemplaza_condiciones()
    {
        var gate = new QualityGate("Old Name", false);
        gate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 80, true);

        var gateId = gate.Id;
        _gates.GetWithConditionsAsync(gateId, Arg.Any<CancellationToken>()).Returns(gate);

        var handler = new UpdateQualityGateCommandHandler(_gates, _uow);
        var command = new UpdateQualityGateCommand(
            gateId,
            "Updated Gate",
            [new ConditionInput(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95, true)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Updated Gate");
        result.Value.Conditions.Should().ContainSingle(c => c.Threshold == 95);
    }

    [Fact]
    public async Task Elimina_el_gate_marcandolo_como_borrado()
    {
        var gate = new QualityGate("Test Gate", false);
        _gates.GetByIdAsync(gate.Id, Arg.Any<CancellationToken>()).Returns(gate);

        var handler = new DeleteQualityGateCommandHandler(_gates, _uow);
        var result = await handler.Handle(new DeleteQualityGateCommand(gate.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        gate.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Elimina_gate_inexistente_lanza_NotFoundException()
    {
        _gates.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((QualityGate?)null);

        var handler = new DeleteQualityGateCommandHandler(_gates, _uow);
        var act = () => handler.Handle(new DeleteQualityGateCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
