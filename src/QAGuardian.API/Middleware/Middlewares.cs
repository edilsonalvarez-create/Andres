using System.Text.Json;
using QAGuardian.Application.Common.Exceptions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Infrastructure.Persistence;

namespace QAGuardian.API.Middleware;

/// <summary>Traduce excepciones de dominio/aplicación a respuestas HTTP consistentes (RFC 7807).</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Error de validación", "Uno o más campos son inválidos.", ex.Errors);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                "Recurso no encontrado", ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Regla de negocio violada", ex.Message);
        }
        catch (ForbiddenAccessException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                "Acceso denegado", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            // No exponer detalles internos (OWASP A05).
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Error interno", "Ocurrió un error inesperado. Contacte al administrador.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title,
        string detail, object? errors = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            detail,
            errors,
            traceId = context.TraceIdentifier
        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
    }
}

/// <summary>Registra en auditoría toda operación de escritura autenticada (ISO 27001 A.12.4).</summary>
public class AuditMiddleware
{
    private static readonly string[] AuditedMethods = ["POST", "PUT", "PATCH", "DELETE"];
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, QAGuardianDbContext db)
    {
        await _next(context);

        if (!AuditedMethods.Contains(context.Request.Method)) return;
        if (context.Response.StatusCode >= 400) return;
        if (context.User.Identity?.IsAuthenticated != true) return;

        try
        {
            var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "desconocido";
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? context.User.FindFirst("sub")?.Value;
            Guid? userId = Guid.TryParse(userIdClaim, out var parsed) ? parsed : null;

            db.AuditLogs.Add(new AuditLog(
                userId, email,
                context.Request.Method,
                context.Request.Path.ToString(),
                null, null, null,
                context.Connection.RemoteIpAddress?.ToString()));
            await db.SaveChangesAsync();
        }
        catch
        {
            // La auditoría nunca debe romper la respuesta ya emitida.
        }
    }
}
