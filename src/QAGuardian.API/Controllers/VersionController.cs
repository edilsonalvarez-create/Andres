using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QAGuardian.API.Controllers;

/// <summary>Metadatos de release para monitoreo y Go Live.</summary>
[AllowAnonymous]
[ApiController]
[Route("api/v{version:apiVersion}/version")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var asm = Assembly.GetExecutingAssembly();
        var informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var fileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        return Ok(new
        {
            product = "QA Guardian",
            version = informational ?? fileVersion ?? "1.0.0-rc.1",
            releaseCandidate = "1.0.0-rc.1",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            commit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? Environment.GetEnvironmentVariable("GITHUB_SHA"),
            serverTimeUtc = DateTime.UtcNow
        });
    }
}
