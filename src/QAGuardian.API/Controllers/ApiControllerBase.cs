using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Convierte un Result de aplicación en respuesta HTTP.</summary>
    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
}
