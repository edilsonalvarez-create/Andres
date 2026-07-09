using System.IdentityModel.Tokens.Jwt;

namespace QAGuardian.Infrastructure.Identity;

/// <summary>
/// Decide a qué esquema de autenticación enrutar una petición: si el JWT recibido
/// fue emitido por un emisor distinto al local, se valida contra el proveedor
/// OIDC externo (OAuth2/OpenID Connect). Solo lee el emisor; la validación
/// criptográfica la hace el esquema correspondiente.
/// </summary>
public static class OidcTokenInspector
{
    public static bool IsExternalToken(string? token, string localIssuer)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var raw = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token["Bearer ".Length..].Trim()
            : token.Trim();

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(raw)) return false;

        try
        {
            var jwt = handler.ReadJwtToken(raw);
            return !string.Equals(jwt.Issuer, localIssuer, StringComparison.Ordinal);
        }
        catch
        {
            // Token ilegible: que lo rechace el esquema local.
            return false;
        }
    }
}
