using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QAGuardian.Infrastructure;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Sprint 20-A (B5): PrepareSchemaIfNecessary configurable; default false fuera de Dev.</summary>
public class HangfirePrepareSchemaTests
{
    [Fact]
    public void Default_sin_clave_es_false()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        DependencyInjection.ResolveHangfirePrepareSchemaIfNecessary(config).Should().BeFalse();
    }

    [Fact]
    public void Production_explicito_false()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:PrepareSchemaIfNecessary"] = "false"
            })
            .Build();

        DependencyInjection.ResolveHangfirePrepareSchemaIfNecessary(config).Should().BeFalse();
    }

    [Fact]
    public void Development_puede_habilitar_true()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hangfire:PrepareSchemaIfNecessary"] = "true"
            })
            .Build();

        DependencyInjection.ResolveHangfirePrepareSchemaIfNecessary(config).Should().BeTrue();
    }
}
