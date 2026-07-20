using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.Infrastructure.Security;

/// <summary>
/// Allowlist de destinos SQL server-side (Sprint 18 / B2). Envuelve el criterio SSRF de
/// <see cref="SsrfGuard"/> (metadata, RFC1918, loopback, link-local, ULA, IP decimal,
/// DNS a privadas) para el DataSource de connection strings configuradas por usuarios:
/// - Host en <c>DatabaseValidation:AllowedSqlHosts</c> → permitido (aunque resuelva a privada).
/// - Allowlist vacía en Development → solo localhost/127.0.0.1 (destino local documentado).
/// - Allowlist vacía fuera de Development → denegar todo (fail-closed).
/// - Allowlist configurada y host no listado → se aplica el mismo criterio de SsrfGuard.
/// Nunca loguea la connection string completa; solo el host rechazado.
/// </summary>
public sealed class SqlHostGuard : ISqlHostGuard
{
    /// <summary>
    /// Aliases locales permitidos en Development con allowlist vacía (localhost documentado).
    /// Incluye los nombres especiales de SqlClient para instancias locales.
    /// </summary>
    private static readonly HashSet<string> DevelopmentLocalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        ".",
        "(local)",
        "(localdb)",
    };

    private readonly IHostAddressResolver _dns;
    private readonly ILogger<SqlHostGuard> _logger;
    private readonly bool _isDevelopment;
    private readonly List<string> _allowedHosts;

    public SqlHostGuard(
        IHostAddressResolver dns,
        IOptions<DatabaseValidationOptions> options,
        IHostEnvironment environment,
        ILogger<SqlHostGuard> logger)
    {
        _dns = dns;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
        _allowedHosts = (options.Value.AllowedSqlHosts ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .ToList();
    }

    public Result<string> ValidateConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return Result<string>.Failure("La connection string es obligatoria.");

        string dataSource;
        try
        {
            dataSource = new SqlConnectionStringBuilder(connectionString.Trim()).DataSource;
        }
        catch (Exception)
        {
            // No se incluye el detalle: la connection string puede contener credenciales.
            return Result<string>.Failure("La connection string no es válida para SQL Server.");
        }

        if (string.IsNullOrWhiteSpace(dataSource))
            return Result<string>.Failure("La connection string no especifica servidor (Data Source).");

        var host = ExtractHost(dataSource);
        if (string.IsNullOrWhiteSpace(host))
            return Result<string>.Failure("No se pudo determinar el host del Data Source.");

        if (IsAllowlisted(host))
            return Result<string>.Success(host);

        if (_allowedHosts.Count == 0)
        {
            if (_isDevelopment && DevelopmentLocalHosts.Contains(host))
                return Result<string>.Success(host);

            return Reject(host, _isDevelopment
                ? "En Development sin allowlist solo se permite localhost como destino SQL."
                : "No hay destinos SQL permitidos configurados (DatabaseValidation:AllowedSqlHosts). Acceso denegado por defecto.");
        }

        // Allowlist configurada y sin coincidencia → mismo criterio SSRF que SsrfGuard.
        if (SsrfGuard.IsBlockedHostName(host))
            return Reject(host, "Host de metadata cloud bloqueado (SSRF).");

        if (SsrfGuard.TryParseHostAsIp(host, out var literalIp))
        {
            return SsrfGuard.IsBlockedAddress(literalIp)
                ? Reject(host, "El destino SQL es una dirección privada, loopback, link-local o de metadata.")
                : Result<string>.Success(host);
        }

        if (SsrfGuard.TryParseDecimalIpv4(host, out var decimalIp))
        {
            return SsrfGuard.IsBlockedAddress(decimalIp)
                ? Reject(host, "El destino SQL es una dirección privada codificada en decimal.")
                : Result<string>.Success(host);
        }

        IPAddress[] addresses;
        try
        {
            addresses = _dns.GetAddresses(host);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DNS falló para destino SQL {Host}", host);
            return Reject(host, "No se pudo resolver el host del destino SQL.");
        }

        if (addresses is null || addresses.Length == 0)
            return Reject(host, "El host del destino SQL no resolvió ninguna dirección.");

        foreach (var address in addresses)
        {
            if (SsrfGuard.IsBlockedAddress(address))
                return Reject(host,
                    "El destino SQL resuelve a una dirección no permitida: red privada, loopback, link-local o metadata.");
        }

        return Result<string>.Success(host);
    }

    /// <summary>
    /// Extrae el host de un DataSource de SqlClient: recorta prefijo de protocolo
    /// (<c>tcp:</c>, <c>np:</c>, ...), puerto (<c>,1433</c>) e instancia (<c>\SQLEXPRESS</c>).
    /// </summary>
    public static string ExtractHost(string dataSource)
    {
        var host = dataSource.Trim();

        foreach (var prefix in new[] { "tcp:", "np:", "lpc:", "admin:" })
        {
            if (host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                host = host[prefix.Length..];
                break;
            }
        }

        // Named pipes: \\server\pipe\... → server.
        if (host.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var segments = host.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0].Trim() : "";
        }

        // Puerto: "host,1433" (el puerto va después de la instancia si hay ambos).
        var commaIndex = host.IndexOf(',');
        if (commaIndex >= 0)
            host = host[..commaIndex];

        // Instancia: "host\SQLEXPRESS".
        var slashIndex = host.IndexOf('\\');
        if (slashIndex >= 0)
            host = host[..slashIndex];

        host = host.Trim();

        // IPv6 entre corchetes: "[::1]" → "::1" para comparar contra allowlist/aliases.
        if (host.StartsWith('[') && host.EndsWith(']'))
            host = host[1..^1];

        return host;
    }

    /// <summary>Coincidencia exacta (case-insensitive) o comodín de sufijo <c>*.dominio</c>.</summary>
    private bool IsAllowlisted(string host)
    {
        foreach (var entry in _allowedHosts)
        {
            if (entry.StartsWith("*.", StringComparison.Ordinal))
            {
                if (host.EndsWith(entry[1..], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(entry, host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private Result<string> Reject(string host, string reason)
    {
        _logger.LogWarning("Destino SQL rechazado (B2): host {Host} — {Reason}", host, reason);
        return Result<string>.Failure($"Destino SQL no permitido ('{host}'): {reason}");
    }
}
