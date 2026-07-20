using QAGuardian.Application.Common.Models;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>
/// Opciones de validación de bases de datos (Sprint 18 / B2).
/// Sección de configuración: <c>DatabaseValidation</c>.
/// </summary>
public sealed class DatabaseValidationOptions
{
    public const string SectionName = "DatabaseValidation";

    /// <summary>
    /// Allowlist de destinos SQL permitidos para entornos de BD por proyecto.
    /// Acepta hosts exactos (<c>sql01.corp.local</c>) o comodín de sufijo (<c>*.corp.local</c>).
    /// Un host allowlisted se permite aunque resuelva a red privada (SQL internos legítimos).
    /// Semántica fail-closed: lista vacía fuera de Development = denegar todo;
    /// en Development lista vacía = solo localhost/127.0.0.1 (documentado en SqlHostGuard).
    /// </summary>
    public string[] AllowedSqlHosts { get; set; } = [];
}

/// <summary>
/// Valida el destino (DataSource) de connection strings SQL configuradas por usuarios
/// contra SSRF (Sprint 18 / B2). Reutiliza el criterio de <see cref="ISsrfGuard"/>:
/// bloquea metadata cloud, RFC1918, loopback, link-local y ULA salvo allowlist explícita
/// (<c>DatabaseValidation:AllowedSqlHosts</c>). No abre ninguna conexión; solo decide.
/// </summary>
public interface ISqlHostGuard
{
    /// <summary>Valida el host de una connection string SQL. Éxito → host validado (sin instancia/puerto).</summary>
    Result<string> ValidateConnectionString(string? connectionString);
}
