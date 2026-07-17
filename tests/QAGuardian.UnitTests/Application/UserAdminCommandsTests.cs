using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Users;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class UserAdminCommandsTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private User SeedUser()
    {
        var u = new User("qa@test.com", "QA Tester", "hash");
        _users.GetByIdAsync(u.Id, Arg.Any<CancellationToken>()).Returns(u);
        return u;
    }

    // ── Protección de la propia cuenta (regla de negocio no trivial) ────

    [Fact]
    public async Task No_puede_desactivar_su_propia_cuenta()
    {
        var u = SeedUser();
        _currentUser.UserId.Returns(u.Id);
        var handler = new SetUserActiveCommandHandler(_users, _currentUser, _uow);

        var result = await handler.Handle(new SetUserActiveCommand(u.Id, Active: false), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("propia");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Puede_reactivar_su_propia_cuenta_no_es_bloqueo()
    {
        // El bloqueo es solo para DESactivar; reactivar la propia cuenta es válido.
        var u = SeedUser();
        u.Deactivate();
        _currentUser.UserId.Returns(u.Id);
        var handler = new SetUserActiveCommandHandler(_users, _currentUser, _uow);

        var result = await handler.Handle(new SetUserActiveCommand(u.Id, Active: true), default);

        result.IsSuccess.Should().BeTrue();
        u.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Desactivar_a_otro_usuario_funciona()
    {
        var u = SeedUser();
        _currentUser.UserId.Returns(Guid.NewGuid()); // admin distinto
        var handler = new SetUserActiveCommandHandler(_users, _currentUser, _uow);

        var result = await handler.Handle(new SetUserActiveCommand(u.Id, Active: false), default);

        result.IsSuccess.Should().BeTrue();
        u.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task No_puede_eliminar_su_propia_cuenta()
    {
        var u = SeedUser();
        _currentUser.UserId.Returns(u.Id);
        var handler = new DeleteUserCommandHandler(_users, _currentUser, _uow);

        var result = await handler.Handle(new DeleteUserCommand(u.Id), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("propia");
    }

    [Fact]
    public async Task Eliminar_otro_usuario_marca_borrado_y_desactiva()
    {
        var u = SeedUser();
        _currentUser.UserId.Returns(Guid.NewGuid());
        var handler = new DeleteUserCommandHandler(_users, _currentUser, _uow);

        var result = await handler.Handle(new DeleteUserCommand(u.Id), default);

        result.IsSuccess.Should().BeTrue();
        u.IsDeleted.Should().BeTrue();
        u.IsActive.Should().BeFalse();
    }

    // ── Reset de contraseña por admin ───────────────────────────────────

    [Fact]
    public async Task Reset_de_contraseña_hashea_la_nueva()
    {
        var u = SeedUser();
        _hasher.Hash("Nueva.Clave.2026").Returns("hash-nuevo");
        var handler = new AdminResetPasswordCommandHandler(_users, _hasher, _uow);

        var result = await handler.Handle(new AdminResetPasswordCommand(u.Id, "Nueva.Clave.2026"), default);

        result.IsSuccess.Should().BeTrue();
        u.PasswordHash.Should().Be("hash-nuevo");
        _hasher.Received(1).Hash("Nueva.Clave.2026");
    }

    [Fact]
    public async Task Reset_a_usuario_inexistente_falla_sin_persistir()
    {
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new AdminResetPasswordCommandHandler(_users, _hasher, _uow);

        var result = await handler.Handle(new AdminResetPasswordCommand(Guid.NewGuid(), "Nueva.Clave.2026"), default);

        result.IsSuccess.Should().BeFalse();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Actualización de roles ──────────────────────────────────────────

    [Fact]
    public async Task Actualizar_roles_con_rol_inexistente_falla()
    {
        var u = SeedUser();
        _users.GetRoleByNameAsync("RolFantasma", Arg.Any<CancellationToken>()).Returns((Role?)null);
        var handler = new UpdateUserRolesCommandHandler(_users, _uow);

        var result = await handler.Handle(new UpdateUserRolesCommand(u.Id, ["RolFantasma"]), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("RolFantasma");
    }

    [Fact]
    public async Task Actualizar_roles_asigna_el_rol_valido()
    {
        var u = SeedUser();
        var role = new Role("QA", "Quality Assurance");
        _users.GetRoleByNameAsync("QA", Arg.Any<CancellationToken>()).Returns(role);
        var handler = new UpdateUserRolesCommandHandler(_users, _uow);

        var result = await handler.Handle(new UpdateUserRolesCommand(u.Id, ["QA"]), default);

        result.IsSuccess.Should().BeTrue();
        u.Roles.Should().Contain(r => r.Name == "QA");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_roles_de_usuario_inexistente_lanza_NotFound()
    {
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new UpdateUserRolesCommandHandler(_users, _uow);

        var act = () => handler.Handle(new UpdateUserRolesCommand(Guid.NewGuid(), ["QA"]), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
