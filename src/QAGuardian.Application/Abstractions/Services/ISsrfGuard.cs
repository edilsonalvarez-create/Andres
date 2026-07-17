using System.Net;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>
/// Valida URIs de salida controladas por el usuario contra SSRF (Sprint 12 / OWASP A10).
/// No realiza la petición HTTP; solo decide si el destino es seguro.
/// </summary>
public interface ISsrfGuard
{
    /// <summary>Valida una URI de salida. Éxito → URI absoluta normalizada.</summary>
    Result<Uri> ValidateOutboundUri(string? uri);

    /// <summary>Valida una URI de salida ya parseada.</summary>
    Result<Uri> ValidateOutboundUri(Uri? uri);
}

/// <summary>Resuelve hosts a direcciones IP (abstraído para tests y DNS rebinding básico).</summary>
public interface IHostAddressResolver
{
    IPAddress[] GetAddresses(string host);
}
