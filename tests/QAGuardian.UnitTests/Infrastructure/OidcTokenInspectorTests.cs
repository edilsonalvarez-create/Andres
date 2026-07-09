using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using QAGuardian.Infrastructure.Identity;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class OidcTokenInspectorTests
{
    private const string LocalIssuer = "QAGuardian";

    private static string MakeToken(string issuer)
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            claims: [new System.Security.Claims.Claim("email", "qa@test.com")]));

    [Fact]
    public void Token_del_emisor_local_va_al_esquema_local()
    {
        var token = MakeToken(LocalIssuer);
        OidcTokenInspector.IsExternalToken($"Bearer {token}", LocalIssuer).Should().BeFalse();
    }

    [Fact]
    public void Token_de_emisor_externo_va_al_esquema_oidc()
    {
        var token = MakeToken("https://login.microsoftonline.com/tenant/v2.0");
        OidcTokenInspector.IsExternalToken($"Bearer {token}", LocalIssuer).Should().BeTrue();
    }

    [Fact]
    public void Acepta_el_token_sin_prefijo_bearer()
    {
        var token = MakeToken("https://accounts.google.com");
        OidcTokenInspector.IsExternalToken(token, LocalIssuer).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer no-es-un-jwt")]
    [InlineData("basura-total")]
    public void Tokens_ilegibles_o_vacios_van_al_esquema_local(string? token)
    {
        OidcTokenInspector.IsExternalToken(token, LocalIssuer).Should().BeFalse();
    }
}
