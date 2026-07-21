using System.Net;
using System.Net.Sockets;
using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Security;

/// <summary>
/// DelegatingHandler que aplica <see cref="ISsrfGuard"/> a cada request del HttpClient
/// (defense-in-depth para el cliente "notifications" y SonarQube).
/// Sprint 18-B (B3): el primary handler lleva <c>AllowAutoRedirect=false</c>, así que los
/// 3xx llegan aquí y se siguen manualmente revalidando la <c>Location</c> con el guard
/// SSRF en CADA hop, con un máximo de <see cref="MaxRedirectHops"/> saltos.
/// </summary>
public sealed class SsrfOutboundHandler : DelegatingHandler
{
    /// <summary>Máximo de redirects 3xx que se siguen por request.</summary>
    public const int MaxRedirectHops = 3;

    private readonly ISsrfGuard _ssrf;

    public SsrfOutboundHandler(ISsrfGuard ssrf) => _ssrf = ssrf;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var check = _ssrf.ValidateOutboundUri(request.RequestUri);
        if (!check.IsSuccess)
            throw new HttpRequestException($"SSRF bloqueado: {check.Error}");

        var response = await base.SendAsync(request, cancellationToken);
        var hops = 0;

        while (IsRedirect(response.StatusCode))
        {
            var location = response.Headers.Location;
            if (location is null)
                return response; // 3xx sin Location: se devuelve tal cual al llamador.

            var previousUri = request.RequestUri!;
            var target = location.IsAbsoluteUri ? location : new Uri(previousUri, location);

            if (++hops > MaxRedirectHops)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"SSRF bloqueado: la cadena de redirects excede el máximo de {MaxRedirectHops} saltos.");
            }

            var hopCheck = _ssrf.ValidateOutboundUri(target);
            if (!hopCheck.IsSuccess)
            {
                response.Dispose();
                throw new HttpRequestException(
                    $"SSRF bloqueado en redirect hacia '{target}': {hopCheck.Error}");
            }

            var statusCode = response.StatusCode;
            response.Dispose();
            request = BuildRedirectRequest(request, statusCode, target);
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private static bool IsRedirect(HttpStatusCode status) => status
        is HttpStatusCode.MovedPermanently // 301
        or HttpStatusCode.Found // 302
        or HttpStatusCode.SeeOther // 303
        or HttpStatusCode.TemporaryRedirect // 307
        or HttpStatusCode.PermanentRedirect; // 308

    /// <summary>
    /// Construye la request del siguiente hop: 301/302/303 degradan a GET sin body
    /// (comportamiento estándar de HttpClient); 307/308 preservan método y contenido.
    /// El header Authorization NO se propaga a un host distinto.
    /// </summary>
    private static HttpRequestMessage BuildRedirectRequest(
        HttpRequestMessage previous, HttpStatusCode status, Uri target)
    {
        var keepMethod = status is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
        var next = new HttpRequestMessage(keepMethod ? previous.Method : HttpMethod.Get, target);
        if (keepMethod)
            next.Content = previous.Content;

        var sameHost = string.Equals(
            previous.RequestUri!.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase);
        foreach (var header in previous.Headers)
        {
            if (!sameHost && header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            next.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return next;
    }
}

/// <summary>
/// Crea el <see cref="SocketsHttpHandler"/> endurecido para los HttpClient con guard SSRF:
/// - <c>AllowAutoRedirect=false</c>: los 3xx los revalida <see cref="SsrfOutboundHandler"/>.
/// - <c>ConnectCallback</c> con pin de IP: resuelve el host UNA vez vía
///   <see cref="ISsrfGuard.ResolvePinnedAddress"/>, valida la IP y conecta a ESA dirección,
///   eliminando la segunda resolución DNS divergente (rebinding TOCTOU). La request sigue
///   usando el hostname original, por lo que SNI y el header Host no cambian.
/// </summary>
public static class SsrfHttpHandlerFactory
{
    public static SocketsHttpHandler Create(ISsrfGuard ssrf) => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var endpoint = ResolvePinnedEndpoint(ssrf, context.DnsEndPoint);
            var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await socket.ConnectAsync(endpoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    /// <summary>Resuelve y valida la IP fijada para un endpoint DNS (expuesto para tests).</summary>
    public static IPEndPoint ResolvePinnedEndpoint(ISsrfGuard ssrf, DnsEndPoint dnsEndPoint)
    {
        var pinned = ssrf.ResolvePinnedAddress(dnsEndPoint.Host);
        if (!pinned.IsSuccess)
            throw new HttpRequestException($"SSRF bloqueado (pin de IP): {pinned.Error}");

        return new IPEndPoint(pinned.Value!, dnsEndPoint.Port);
    }
}
