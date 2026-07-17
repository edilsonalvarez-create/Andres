using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QAGuardian.Domain.Entities;
using QAGuardian.Infrastructure.Identity;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Regresión Sprint 2 (OWASP A02:2025): la clave de firma HS256 debe tener al menos
/// 256 bits; una clave corta permitiría forjar tokens por fuerza bruta.</summary>
public class JwtTokenServiceTests
{
    private static IConfiguration BuildConfig(string signingKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = signingKey })
            .Build();

    [Fact]
    public void Rechaza_una_clave_de_firma_demasiado_corta()
    {
        var act = () => new JwtTokenService(BuildConfig("muy-corta"));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*al menos 32 bytes*");
    }

    [Fact]
    public void Rechaza_una_clave_vacia()
    {
        var act = () => new JwtTokenService(BuildConfig(""));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Acepta_una_clave_de_32_bytes_exactos()
    {
        var key32Bytes = new string('a', 32);
        var act = () => new JwtTokenService(BuildConfig(key32Bytes));
        act.Should().NotThrow();
    }

    [Fact]
    public void Genera_un_access_token_valido_con_clave_suficiente()
    {
        var service = new JwtTokenService(BuildConfig(new string('a', 32)));
        var user = new User("qa@test.com", "QA Tester", "hash-irrelevante-aqui");

        var token = service.GenerateAccessToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Should().HaveCount(3); // header.payload.signature
    }

    [Fact]
    public void Los_refresh_tokens_generados_son_unicos()
    {
        var service = new JwtTokenService(BuildConfig(new string('a', 32)));

        var t1 = service.GenerateRefreshToken();
        var t2 = service.GenerateRefreshToken();

        t1.Should().NotBe(t2);
    }
}
