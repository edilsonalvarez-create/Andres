using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Application.Features.DatabaseValidation;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

/// <summary>
/// Sprint 18-A (B2): el Upsert de entornos de BD valida el destino SQL con
/// <see cref="ISqlHostGuard"/> ANTES de cifrar y persistir la connection string.
/// </summary>
public class Sprint18SqlHostAllowlistTests
{
    private readonly IProjectDatabaseEnvironmentRepository _envs =
        Substitute.For<IProjectDatabaseEnvironmentRepository>();
    private readonly ITokenEncryptionService _encryption = Substitute.For<ITokenEncryptionService>();
    private readonly ISqlHostGuard _sqlHostGuard = Substitute.For<ISqlHostGuard>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private UpsertProjectDatabaseEnvironmentCommandHandler CreateHandler()
        => new(_envs, _encryption, _sqlHostGuard, _access, _uow);

    [Fact]
    public async Task Upsert_rechaza_destino_bloqueado_sin_cifrar_ni_persistir()
    {
        _sqlHostGuard.ValidateConnectionString(Arg.Any<string>())
            .Returns(Result<string>.Failure("Destino SQL no permitido ('169.254.169.254'): metadata."));

        var result = await CreateHandler().Handle(
            new UpsertProjectDatabaseEnvironmentCommand(
                Guid.NewGuid(), "staging", "Server=169.254.169.254;Database=QA;User Id=sa;Password=x"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("169.254.169.254");
        _encryption.DidNotReceiveWithAnyArgs().Encrypt(default!);
        await _envs.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Upsert_persiste_cuando_el_destino_esta_permitido()
    {
        _sqlHostGuard.ValidateConnectionString(Arg.Any<string>())
            .Returns(Result<string>.Success("sql-interno.corp.local"));
        _encryption.Encrypt(Arg.Any<string>()).Returns("cipher");
        _envs.GetByNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ProjectDatabaseEnvironment?)null);

        var result = await CreateHandler().Handle(
            new UpsertProjectDatabaseEnvironmentCommand(
                Guid.NewGuid(), "staging", "Server=sql-interno.corp.local;Database=QA;User Id=app;Password=x"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        await _envs.ReceivedWithAnyArgs(1).AddAsync(default!, default);
        await _uow.ReceivedWithAnyArgs(1).SaveChangesAsync(default);
    }
}
