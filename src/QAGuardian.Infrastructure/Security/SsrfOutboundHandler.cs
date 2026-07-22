using QAGuardian.Application.Abstractions.Services;

namespace QAGuardian.Infrastructure.Security;

/// <summary>
/// DelegatingHandler que aplica <see cref="ISsrfGuard"/> a cada request del HttpClient
/// (defense-in-depth para el cliente "notifications" y SonarQube).
/// </summary>
public sealed class SsrfOutboundHandler : DelegatingHandler
{
    private readonly ISsrfGuard _ssrf;

    public SsrfOutboundHandler(ISsrfGuard ssrf) => _ssrf = ssrf;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var check = _ssrf.ValidateOutboundUri(request.RequestUri);
        if (!check.IsSuccess)
            throw new HttpRequestException($"SSRF bloqueado: {check.Error}");

        return base.SendAsync(request, cancellationToken);
    }
}
