using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QAGuardian.Application.Features.Auth;

namespace QAGuardian.API.Controllers;

/// <summary>
/// Autenticación. OWASP A05:2025 (Security Misconfiguration) / A01:2025 (Broken Access Control):
/// el refresh token viaja únicamente en una cookie httpOnly+Secure+SameSite=Strict, con alcance
/// (Path) restringido a este controlador — un XSS en cualquier otra parte del SPA no puede leerlo
/// ni exfiltrarlo. El access token (de vida corta, 30 min) sigue viajando en el cuerpo JSON para
/// que el SPA lo mantenga en memoria y lo use como Bearer; no se persiste en el servidor más allá
/// de la firma JWT. Ver Security-Report.md (hallazgo SEC-03) para el detalle del rediseño.
/// </summary>
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private const string RefreshCookieName = "qaguardian_rt";
    private const string CsrfCookieName = "qaguardian_csrf";
    private const string CsrfHeaderName = "X-CSRF-Token";
    private const string AuthCookiePath = "/api/v1/auth";

    /// <summary>Inicia sesión: emite el access token en el cuerpo y el refresh token en una cookie httpOnly.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command, [FromServices] IHostEnvironment env, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });

        SetAuthCookies(result.Value!.RefreshToken, env);
        return Ok(ToPublicResponse(result.Value));
    }

    /// <summary>Renueva el access token usando el refresh token de la cookie httpOnly (rotación + CSRF double-submit).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromServices] IHostEnvironment env, CancellationToken ct)
    {
        if (!TryValidateCsrf(out var csrfError))
            return Unauthorized(new { error = csrfError });

        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { error = "No hay una sesión activa." });

        var result = await Mediator.Send(new RefreshTokenCommand(refreshToken), ct);
        if (!result.IsSuccess)
        {
            ClearAuthCookies();
            return Unauthorized(new { error = result.Error });
        }

        SetAuthCookies(result.Value!.RefreshToken, env);
        return Ok(ToPublicResponse(result.Value));
    }

    /// <summary>Cierra la sesión: revoca el refresh token vigente y limpia las cookies (CSRF double-submit).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!TryValidateCsrf(out var csrfError))
            return Unauthorized(new { error = csrfError });

        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
            await Mediator.Send(new LogoutCommand(refreshToken), ct);

        ClearAuthCookies();
        return Ok(new { message = "Sesión cerrada." });
    }

    /// <summary>Registra un nuevo usuario (solo administradores).</summary>
    [HttpPost("register")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    /// <summary>Cambia la contraseña del usuario autenticado (autoservicio).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));

    // ── Helpers de cookies / CSRF ────────────────────────────────────────

    /// <summary>DTO público: nunca incluye el refresh token (solo viaja en la cookie httpOnly).</summary>
    private static object ToPublicResponse(AuthResponseDto auth) => new
    {
        accessToken = auth.AccessToken,
        expiresInMinutes = auth.ExpiresInMinutes,
        userId = auth.UserId,
        email = auth.Email,
        fullName = auth.FullName,
        roles = auth.Roles
    };

    private void SetAuthCookies(string refreshToken, IHostEnvironment env)
    {
        var secure = !env.IsDevelopment();

        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = AuthCookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        // Cookie legible por JS a propósito (double-submit CSRF): el SPA la lee y la reenvía como
        // encabezado; un atacante cross-site no puede leer cookies de este origen para igualarla.
        Response.Cookies.Append(CsrfCookieName, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            new CookieOptions
            {
                HttpOnly = false,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = AuthCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = AuthCookiePath });
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions { Path = AuthCookiePath });
    }

    private bool TryValidateCsrf(out string? error)
    {
        error = null;
        if (!Request.Cookies.TryGetValue(CsrfCookieName, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
        {
            error = "Falta el token CSRF.";
            return false;
        }

        var headerValue = Request.Headers[CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(headerValue)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(cookieValue), System.Text.Encoding.UTF8.GetBytes(headerValue)))
        {
            error = "Token CSRF inválido.";
            return false;
        }

        return true;
    }
}
