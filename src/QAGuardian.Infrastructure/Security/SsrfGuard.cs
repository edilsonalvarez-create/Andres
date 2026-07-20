using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;

namespace QAGuardian.Infrastructure.Security;

/// <summary>
/// Guard SSRF reutilizable (UrlSafety). Bloquea privados, loopback, link-local,
/// metadata cloud y esquemas no HTTPS (salvo allowlist explícita de desarrollo).
/// </summary>
public sealed class SsrfGuard : ISsrfGuard
{
    private static readonly HashSet<string> BlockedHostNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "metadata.google.internal",
        "metadata.goog",
        "metadata",
        "kubernetes.default",
        "kubernetes.default.svc",
    };

    private readonly IHostAddressResolver _dns;
    private readonly ILogger<SsrfGuard> _logger;
    private readonly HashSet<string> _allowHttpHosts;
    private readonly HashSet<string> _allowPrivateHosts;

    public SsrfGuard(
        IHostAddressResolver dns,
        IConfiguration configuration,
        ILogger<SsrfGuard> logger)
    {
        _dns = dns;
        _logger = logger;
        _allowHttpHosts = ReadHostSet(configuration, "Security:Ssrf:AllowHttpHosts");
        _allowPrivateHosts = ReadHostSet(configuration, "Security:Ssrf:AllowPrivateHosts");
        // HTTP allowlist implica destino de desarrollo local: también puede ser privado.
        foreach (var host in _allowHttpHosts)
            _allowPrivateHosts.Add(host);
    }

    public Result<Uri> ValidateOutboundUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return Result<Uri>.Failure("La URL de salida es obligatoria.");

        if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var parsed))
            return Result<Uri>.Failure("La URL de salida no es una URI absoluta válida.");

        return ValidateOutboundUri(parsed);
    }

    public Result<Uri> ValidateOutboundUri(Uri? uri)
    {
        if (uri is null)
            return Result<Uri>.Failure("La URL de salida es obligatoria.");

        if (!uri.IsAbsoluteUri)
            return Result<Uri>.Failure("La URL de salida debe ser absoluta.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return Result<Uri>.Failure("La URL de salida no puede incluir credenciales (userinfo).");

        var host = string.IsNullOrWhiteSpace(uri.IdnHost) ? uri.DnsSafeHost : uri.IdnHost;
        var allowHttp = !string.IsNullOrWhiteSpace(host) && _allowHttpHosts.Contains(host);

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            // ok
        }
        else if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && allowHttp)
        {
            // ok — allowlist explícita de desarrollo
        }
        else if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                 && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Uri>.Failure("Solo se permite HTTPS para URLs de salida (salvo allowlist de desarrollo).");
        }
        else
        {
            return Result<Uri>.Failure(
                "Solo se permite HTTPS para URLs de salida (salvo allowlist de desarrollo).");
        }

        if (string.IsNullOrWhiteSpace(host))
            return Result<Uri>.Failure("La URL de salida no tiene host.");

        if (IsBlockedHostName(host))
            return Result<Uri>.Failure("Host de metadata cloud bloqueado (SSRF).");

        if (uri.IsDefaultPort == false && (uri.Port <= 0 || uri.Port > 65535))
            return Result<Uri>.Failure("Puerto de URL inválido.");

        var allowPrivate = _allowPrivateHosts.Contains(host);

        // Literal IP (incluye IPv6 entre corchetes vía IdnHost / DnsSafeHost).
        if (TryParseHostAsIp(host, out var literalIp))
        {
            if (!allowPrivate && IsBlockedAddress(literalIp))
                return FailBlockedIp(literalIp);
            return Result<Uri>.Success(uri);
        }

        // Host numérico tipo 2130706433 → 127.0.0.1 (bypass clásico).
        if (TryParseDecimalIpv4(host, out var decimalIp))
        {
            if (!allowPrivate && IsBlockedAddress(decimalIp))
                return FailBlockedIp(decimalIp);
            return Result<Uri>.Success(uri);
        }

        IPAddress[] addresses;
        try
        {
            addresses = _dns.GetAddresses(host);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DNS falló para host {Host}", host);
            return Result<Uri>.Failure("No se pudo resolver el host de la URL de salida.");
        }

        if (addresses is null || addresses.Length == 0)
            return Result<Uri>.Failure("El host de la URL de salida no resolvió ninguna dirección.");

        if (!allowPrivate)
        {
            foreach (var address in addresses)
            {
                if (IsBlockedAddress(address))
                    return FailBlockedIp(address);
            }
        }

        return Result<Uri>.Success(uri);
    }

    private static Result<Uri> FailBlockedIp(IPAddress address)
        => Result<Uri>.Failure(
            $"La URL de salida resuelve a una dirección no permitida ({address}): red privada, loopback, link-local o metadata.");

    /// <summary>True si el hostname es de metadata cloud u otro nombre interno bloqueado.</summary>
    internal static bool IsBlockedHostName(string host)
        => BlockedHostNames.Contains(host)
           || host.EndsWith(".metadata.google.internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>True si la IP no debe usarse como destino saliente controlado por usuario.</summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        // Normalizar IPv4-mapped IPv6.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsBlockedIpv4(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return IsBlockedIpv6(address);

        return true;
    }

    private static bool IsBlockedIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        // 0.0.0.0/8
        if (bytes[0] == 0)
            return true;
        // 10.0.0.0/8
        if (bytes[0] == 10)
            return true;
        // 127.0.0.0/8 (también cubierto por IsLoopback)
        if (bytes[0] == 127)
            return true;
        // 169.254.0.0/16 link-local + metadata 169.254.169.254
        if (bytes[0] == 169 && bytes[1] == 254)
            return true;
        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return true;
        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
            return true;
        // 100.64.0.0/10 CGNAT
        if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            return true;
        // 192.0.0.0/24 IETF, 192.0.2.0/24 TEST-NET, etc. — bloqueo conservador de no-enrutables comunes
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] <= 2)
            return true;
        // Multicast 224.0.0.0/4
        if (bytes[0] >= 224 && bytes[0] <= 239)
            return true;
        // Broadcast
        if (address.Equals(IPAddress.Broadcast))
            return true;

        return false;
    }

    private static bool IsBlockedIpv6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
            return true;

        var bytes = address.GetAddressBytes();
        // fe80::/10 link-local
        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
            return true;
        // fc00::/7 unique local
        if ((bytes[0] & 0xfe) == 0xfc)
            return true;
        // ff00::/8 multicast
        if (bytes[0] == 0xff)
            return true;

        return false;
    }

    internal static bool TryParseHostAsIp(string host, out IPAddress ip)
    {
        // Quitar corchetes IPv6 si vinieran en Host (IdnHost normalmente no los trae).
        var candidate = host;
        if (candidate.StartsWith('[') && candidate.EndsWith(']'))
            candidate = candidate[1..^1];

        return IPAddress.TryParse(candidate, out ip!);
    }

    internal static bool TryParseDecimalIpv4(string host, out IPAddress ip)
    {
        ip = IPAddress.None;
        if (host.Length == 0 || host.Length > 10 || !ulong.TryParse(host, out var value) || value > uint.MaxValue)
            return false;

        var b0 = (byte)((value >> 24) & 0xff);
        var b1 = (byte)((value >> 16) & 0xff);
        var b2 = (byte)((value >> 8) & 0xff);
        var b3 = (byte)(value & 0xff);
        ip = new IPAddress(new[] { b0, b1, b2, b3 });
        return true;
    }

    private static HashSet<string> ReadHostSet(IConfiguration configuration, string key)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var section = configuration.GetSection(key);
        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                set.Add(child.Value.Trim());
        }

        // También aceptar valor único "a,b,c"
        var flat = configuration[key];
        if (!string.IsNullOrWhiteSpace(flat) && !section.GetChildren().Any())
        {
            foreach (var part in flat.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(part);
        }

        return set;
    }
}

/// <summary>Resolución DNS real vía <see cref="Dns.GetHostAddresses(string)"/>.</summary>
public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public IPAddress[] GetAddresses(string host) => Dns.GetHostAddresses(host);
}
