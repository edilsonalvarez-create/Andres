using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Features.Auth;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application.Auth;

/// <summary>Regresión Sprint 2: el refresh token debe poder revocarse explícitamente (logout),
/// requisito para que la cookie httpOnly pueda invalidarse del lado servidor.</summary>
public class LogoutCommandTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Revoca_un_refresh_token_activo()
    {
        var user = new User("qa@test.com", "QA Tester", "hash");
        var token = user.AddRefreshToken("token-activo", DateTime.UtcNow.AddDays(7));
        _users.GetByRefreshTokenAsync("token-activo", Arg.Any<CancellationToken>()).Returns(user);

        var handler = new LogoutCommandHandler(_users, _uow);
        var result = await handler.Handle(new LogoutCommand("token-activo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        token.IsActive.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Es_idempotente_si_el_token_no_existe()
    {
        _users.GetByRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new LogoutCommandHandler(_users, _uow);
        var result = await handler.Handle(new LogoutCommand("token-inexistente"), CancellationToken.None);

        // No revela si el token existía o no (mismo comportamiento que un logout exitoso).
        result.IsSuccess.Should().BeTrue();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Es_idempotente_si_el_token_ya_estaba_revocado()
    {
        var user = new User("qa@test.com", "QA Tester", "hash");
        var token = user.AddRefreshToken("token-ya-revocado", DateTime.UtcNow.AddDays(7));
        token.Revoke();
        _users.GetByRefreshTokenAsync("token-ya-revocado", Arg.Any<CancellationToken>()).Returns(user);

        var handler = new LogoutCommandHandler(_users, _uow);
        var result = await handler.Handle(new LogoutCommand("token-ya-revocado"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
