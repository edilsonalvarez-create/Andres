using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QAGuardian.Application.Features.Auth;

namespace QAGuardian.API.Controllers;

[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    /// <summary>Inicia sesión y devuelve tokens JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    /// <summary>Renueva el access token usando un refresh token vigente.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    /// <summary>Registra un nuevo usuario (solo administradores).</summary>
    [HttpPost("register")]
    [Authorize(Policy = Policies.Administer)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken ct)
        => FromResult(await Mediator.Send(command, ct));
}
