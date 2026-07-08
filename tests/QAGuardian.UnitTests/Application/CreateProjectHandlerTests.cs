using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Features.Projects;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class CreateProjectHandlerTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IQualityGateRepository _gates = Substitute.For<IQualityGateRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Crea_el_proyecto_y_le_asigna_el_gate_por_defecto()
    {
        _projects.GetByCodeAsync("ERP", Arg.Any<CancellationToken>()).Returns((Project?)null);
        var defaultGate = new QualityGate("Default", isDefault: true);
        defaultGate.AddCondition(GateMetric.PassRatePercent, GateOperator.GreaterOrEqual, 95m);
        _gates.GetDefaultAsync(Arg.Any<CancellationToken>()).Returns(defaultGate);

        var handler = new CreateProjectCommandHandler(_projects, _gates, _uow);
        var result = await handler.Handle(
            new CreateProjectCommand("ERP", "Sistema ERP", "Descripción", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("ERP");
        result.Value.QualityGateId.Should().Be(defaultGate.Id);
        await _projects.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rechaza_codigos_duplicados()
    {
        _projects.GetByCodeAsync("ERP", Arg.Any<CancellationToken>())
            .Returns(new Project("ERP", "Existente", null, null));

        var handler = new CreateProjectCommandHandler(_projects, _gates, _uow);
        var result = await handler.Handle(
            new CreateProjectCommand("ERP", "Otro proyecto", null, null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ERP");
    }
}
