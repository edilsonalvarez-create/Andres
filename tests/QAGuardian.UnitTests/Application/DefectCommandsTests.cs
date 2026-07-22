using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Defects;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class DefectCommandsTests
{
    private readonly IDefectRepository _defects = Substitute.For<IDefectRepository>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly INotificationDispatcher _notifications = Substitute.For<INotificationDispatcher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public DefectCommandsTests()
    {
        _access.CanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private Defect SeedDefect(DefectStatus status = DefectStatus.New)
    {
        var d = new Defect(Guid.NewGuid(), "BUG-1", "Título", "Desc",
            DefectSeverity.Major, DefectPriority.High, Guid.NewGuid());
        // Llevar al estado deseado a través de las transiciones válidas del dominio.
        switch (status)
        {
            case DefectStatus.Assigned: d.Assign(Guid.NewGuid()); break;
            case DefectStatus.InProgress: d.Assign(Guid.NewGuid()); d.StartProgress(); break;
            case DefectStatus.Resolved: d.Assign(Guid.NewGuid()); d.StartProgress(); d.Resolve(); break;
            case DefectStatus.Verified: d.Assign(Guid.NewGuid()); d.StartProgress(); d.Resolve(); d.Verify(); break;
        }
        _defects.GetByIdAsync(d.Id, Arg.Any<CancellationToken>()).Returns(d);
        return d;
    }

    // ── Crear ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_genera_codigo_persiste_y_notifica()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId);
        _defects.NextCodeAsync(projectId, Arg.Any<CancellationToken>()).Returns("BUG-42");

        var handler = new CreateDefectCommandHandler(_defects, _access, _currentUser, _notifications, _uow);
        var result = await handler.Handle(new CreateDefectCommand(
            projectId, "Login roto", "500 al iniciar sesión", DefectSeverity.Critical,
            DefectPriority.High, null, null, "S-12", "1.2.0", "stack"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("BUG-42");
        result.Value.Status.Should().Be(DefectStatus.New);
        await _defects.Received(1).AddAsync(Arg.Any<Defect>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifications.Received(1).DispatchAsync(
            Arg.Is<NotificationMessage>(m => m.Event == NotificationEvents.DefectCreated),
            Arg.Any<CancellationToken>());
    }

    // ── Transiciones de estado (máquina de estados del dominio) ─────────

    [Fact]
    public async Task Asignar_requiere_usuario_destino()
    {
        var d = SeedDefect(DefectStatus.New);
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        var result = await handler.Handle(
            new ChangeDefectStatusCommand(d.Id, DefectStatus.Assigned, null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("usuario");
    }

    [Fact]
    public async Task Asignar_con_usuario_pasa_a_Assigned()
    {
        var d = SeedDefect(DefectStatus.New);
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        var result = await handler.Handle(
            new ChangeDefectStatusCommand(d.Id, DefectStatus.Assigned, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DefectStatus.Assigned);
    }

    [Fact]
    public async Task Ciclo_completo_Assigned_InProgress_Resolved_Verified_Closed()
    {
        var d = SeedDefect(DefectStatus.Assigned);
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        (await handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.InProgress), default)).Value!.Status.Should().Be(DefectStatus.InProgress);
        (await handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.Resolved), default)).Value!.Status.Should().Be(DefectStatus.Resolved);
        (await handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.Verified), default)).Value!.Status.Should().Be(DefectStatus.Verified);
        (await handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.Closed), default)).Value!.Status.Should().Be(DefectStatus.Closed);
        d.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Verificar_un_defecto_nuevo_viola_la_maquina_de_estados()
    {
        var d = SeedDefect(DefectStatus.New);
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        // El handler no captura DomainException: burbujea (la traduce el middleware a 422).
        var act = () => handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.Verified), default);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Reabrir_un_defecto_resuelto_limpia_ResolvedAt()
    {
        var d = SeedDefect(DefectStatus.Resolved);
        d.ResolvedAt.Should().NotBeNull();
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        var result = await handler.Handle(new ChangeDefectStatusCommand(d.Id, DefectStatus.Reopened), default);

        result.Value!.Status.Should().Be(DefectStatus.Reopened);
        d.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Cambiar_estado_de_defecto_inexistente_lanza_NotFound()
    {
        _defects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Defect?)null);
        var handler = new ChangeDefectStatusCommandHandler(_defects, _access, _uow);

        var act = () => handler.Handle(new ChangeDefectStatusCommand(Guid.NewGuid(), DefectStatus.Resolved), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Consulta con filtros ───────────────────────────────────────────

    [Fact]
    public async Task Listar_devuelve_pagina_ordenada_por_fecha_desc()
    {
        var projectId = Guid.NewGuid();
        var older = new Defect(projectId, "BUG-1", "Viejo", "d", DefectSeverity.Minor, DefectPriority.Low, Guid.NewGuid());
        var newer = new Defect(projectId, "BUG-2", "Nuevo", "d", DefectSeverity.Minor, DefectPriority.Low, Guid.NewGuid());
        typeof(AuditableEntity).GetProperty("CreatedAt")!.SetValue(older, DateTime.UtcNow.AddDays(-2));
        typeof(AuditableEntity).GetProperty("CreatedAt")!.SetValue(newer, DateTime.UtcNow);
        _defects.PagedAsync(1, 20, Arg.Any<System.Linq.Expressions.Expression<Func<Defect, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(([older, newer], 2));

        var handler = new GetDefectsQueryHandler(_defects, _access);
        var result = await handler.Handle(new GetDefectsQuery(projectId), default);

        result.TotalCount.Should().Be(2);
        result.Items[0].Code.Should().Be("BUG-2"); // el más reciente primero
    }
}
