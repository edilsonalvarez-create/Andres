using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Auth;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private LoginCommandHandler CreateHandler() => new(_users, _hasher, _jwt, _uow);

    [Fact]
    public async Task Login_valido_devuelve_tokens_y_roles()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        user.AssignRole(new Role(SystemRoles.QA, "QA"));
        _users.GetByEmailAsync("qa@test.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("Password123!", "hash").Returns(true);
        _jwt.GenerateAccessToken(user).Returns("access-token");
        _jwt.GenerateRefreshToken().Returns("refresh-token");
        _jwt.AccessTokenMinutes.Returns(30);
        _jwt.RefreshTokenDays.Returns(7);

        var result = await CreateHandler().Handle(new LoginCommand("qa@test.com", "Password123!"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("access-token");
        result.Value.Roles.Should().Contain(SystemRoles.QA);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Contrasena_incorrecta_devuelve_mensaje_generico_y_registra_intento()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        _users.GetByEmailAsync("qa@test.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await CreateHandler().Handle(new LoginCommand("qa@test.com", "Incorrecta1!"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Credenciales inválidas.");
        user.FailedLoginAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Usuario_inexistente_devuelve_el_mismo_mensaje_generico()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new LoginCommand("nadie@test.com", "Password123!"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Credenciales inválidas.");
    }

    [Fact]
    public async Task Cuenta_bloqueada_no_permite_login()
    {
        var user = new User("qa@test.com", "Usuario QA", "hash");
        for (var i = 0; i < 5; i++) user.RegisterFailedLogin();
        _users.GetByEmailAsync("qa@test.com", Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new LoginCommand("qa@test.com", "Password123!"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("bloqueada");
    }
}
