using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Auth;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class AuthAdditionalCommandsTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    // ── Registro ───────────────────────────────────────────────────────

    [Fact]
    public async Task Registrar_rechaza_correo_ya_existente()
    {
        _users.GetByEmailAsync("dup@test.com", Arg.Any<CancellationToken>())
            .Returns(new User("dup@test.com", "Existente", "hash"));
        var handler = new RegisterUserCommandHandler(_users, _hasher, _uow);

        var result = await handler.Handle(
            new RegisterUserCommand("dup@test.com", "Nuevo", "Passw0rd12", ["QA"]), default);

        result.IsSuccess.Should().BeFalse();
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registrar_con_rol_inexistente_falla()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _users.GetRoleByNameAsync("RolFantasma", Arg.Any<CancellationToken>()).Returns((Role?)null);
        _hasher.Hash(Arg.Any<string>()).Returns("hash");
        var handler = new RegisterUserCommandHandler(_users, _hasher, _uow);

        var result = await handler.Handle(
            new RegisterUserCommand("new@test.com", "Nuevo", "Passw0rd12", ["RolFantasma"]), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("RolFantasma");
    }

    [Fact]
    public async Task Registrar_valido_crea_el_usuario_con_rol()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _users.GetRoleByNameAsync("QA", Arg.Any<CancellationToken>()).Returns(new Role("QA", "Quality"));
        _hasher.Hash("Passw0rd12").Returns("hash-x");
        var handler = new RegisterUserCommandHandler(_users, _hasher, _uow);

        var result = await handler.Handle(
            new RegisterUserCommand("new@test.com", "Nuevo Usuario", "Passw0rd12", ["QA"]), default);

        result.IsSuccess.Should().BeTrue();
        await _users.Received(1).AddAsync(Arg.Is<User>(u => u.Email == "new@test.com"), Arg.Any<CancellationToken>());
    }

    // ── Cambio de contraseña (autoservicio) ─────────────────────────────

    [Fact]
    public async Task Cambiar_contraseña_sin_sesion_falla()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var handler = new ChangePasswordCommandHandler(_users, _hasher, _currentUser, _uow);

        var result = await handler.Handle(new ChangePasswordCommand("vieja", "Nueva.Clave.2026"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("sesión");
    }

    [Fact]
    public async Task Cambiar_contraseña_con_actual_incorrecta_falla()
    {
        var u = new User("qa@test.com", "QA", "hash-actual");
        _currentUser.UserId.Returns(u.Id);
        _users.GetByIdAsync(u.Id, Arg.Any<CancellationToken>()).Returns(u);
        _hasher.Verify("incorrecta", "hash-actual").Returns(false);
        var handler = new ChangePasswordCommandHandler(_users, _hasher, _currentUser, _uow);

        var result = await handler.Handle(new ChangePasswordCommand("incorrecta", "Nueva.Clave.2026"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("actual");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cambiar_contraseña_valida_actualiza_el_hash()
    {
        var u = new User("qa@test.com", "QA", "hash-actual");
        _currentUser.UserId.Returns(u.Id);
        _users.GetByIdAsync(u.Id, Arg.Any<CancellationToken>()).Returns(u);
        _hasher.Verify("actual", "hash-actual").Returns(true);
        _hasher.Hash("Nueva.Clave.2026").Returns("hash-nuevo");
        var handler = new ChangePasswordCommandHandler(_users, _hasher, _currentUser, _uow);

        var result = await handler.Handle(new ChangePasswordCommand("actual", "Nueva.Clave.2026"), default);

        result.IsSuccess.Should().BeTrue();
        u.PasswordHash.Should().Be("hash-nuevo");
    }

    // ── Refresh token (rotación) ───────────────────────────────────────

    [Fact]
    public async Task Refresh_con_token_desconocido_falla()
    {
        _users.GetByRefreshTokenAsync("desconocido", Arg.Any<CancellationToken>()).Returns((User?)null);
        var handler = new RefreshTokenCommandHandler(_users, _jwt, _uow);

        var result = await handler.Handle(new RefreshTokenCommand("desconocido"), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_valido_rota_el_token_y_revoca_el_anterior()
    {
        var u = new User("qa@test.com", "QA", "hash");
        var oldToken = u.AddRefreshToken("token-viejo", DateTime.UtcNow.AddDays(7));
        _users.GetByRefreshTokenAsync("token-viejo", Arg.Any<CancellationToken>()).Returns(u);
        _jwt.RefreshTokenDays.Returns(7);
        _jwt.AccessTokenMinutes.Returns(30);
        _jwt.GenerateAccessToken(u).Returns("nuevo-access");
        _jwt.GenerateRefreshToken().Returns("token-nuevo");
        var handler = new RefreshTokenCommandHandler(_users, _jwt, _uow);

        var result = await handler.Handle(new RefreshTokenCommand("token-viejo"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("nuevo-access");
        oldToken.IsActive.Should().BeFalse(); // el anterior quedó revocado (rotación)
    }
}
