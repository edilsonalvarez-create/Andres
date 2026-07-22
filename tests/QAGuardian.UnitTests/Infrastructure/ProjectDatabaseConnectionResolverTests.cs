using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Infrastructure.Identity;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class ProjectDatabaseConnectionResolverTests
{
    [Fact]
    public async Task ResolveAsync_desencripta_desde_entidad()
    {
        var projectId = Guid.NewGuid();
        var repo = Substitute.For<IProjectDatabaseEnvironmentRepository>();
        var encryption = Substitute.For<ITokenEncryptionService>();
        var config = new ConfigurationBuilder().Build();

        var entity = new ProjectDatabaseEnvironment(projectId, "dev", "cipher-text");
        repo.GetByNameAsync(projectId, "dev", Arg.Any<CancellationToken>()).Returns(entity);
        encryption.Decrypt("cipher-text").Returns("Server=local;Database=Dev;");

        var sut = new ProjectDatabaseConnectionResolver(repo, encryption, config);
        var conn = await sut.ResolveAsync(projectId, "DEV");

        conn.Should().Be("Server=local;Database=Dev;");
    }

    [Fact]
    public async Task ResolveAsync_usa_IConfiguration_si_no_hay_entidad()
    {
        var projectId = Guid.NewGuid();
        var repo = Substitute.For<IProjectDatabaseEnvironmentRepository>();
        var encryption = Substitute.For<ITokenEncryptionService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ProjectDatabaseEnvironments:{projectId}:staging"] =
                    "Server=cfg;Database=Staging;"
            })
            .Build();

        repo.GetByNameAsync(projectId, "staging", Arg.Any<CancellationToken>())
            .Returns((ProjectDatabaseEnvironment?)null);

        var sut = new ProjectDatabaseConnectionResolver(repo, encryption, config);
        var conn = await sut.ResolveAsync(projectId, "staging");

        conn.Should().Be("Server=cfg;Database=Staging;");
        encryption.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task ResolveAsync_lanza_si_no_existe()
    {
        var projectId = Guid.NewGuid();
        var repo = Substitute.For<IProjectDatabaseEnvironmentRepository>();
        var encryption = Substitute.For<ITokenEncryptionService>();
        var config = new ConfigurationBuilder().Build();
        repo.GetByNameAsync(projectId, "prod", Arg.Any<CancellationToken>())
            .Returns((ProjectDatabaseEnvironment?)null);

        var sut = new ProjectDatabaseConnectionResolver(repo, encryption, config);
        var act = () => sut.ResolveAsync(projectId, "prod");
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
