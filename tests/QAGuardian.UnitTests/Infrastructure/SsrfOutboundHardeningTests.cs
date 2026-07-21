using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Clients;
using QAGuardian.Infrastructure.Security;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// Sprint 18-B (B3): endurecimiento de HttpClient outbound —
/// redirects 3xx revalidados por hop, límite de saltos, pin de IP anti DNS rebinding
/// y named client SSRF en IntegrationConnectionTester.
/// </summary>
public class SsrfOutboundHardeningTests
{
    private static readonly IPAddress PublicIp = IPAddress.Parse("93.184.216.34");
    private static readonly IPAddress PrivateIp = IPAddress.Parse("10.1.2.3");

    private readonly IHostAddressResolver _dns = Substitute.For<IHostAddressResolver>();
    private readonly SsrfGuard _ssrf;

    public SsrfOutboundHardeningTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        _ssrf = new SsrfGuard(_dns, config, NullLogger<SsrfGuard>.Instance);
    }

    private HttpClient CreateClient(ScriptedHandler inner)
        => new(new SsrfOutboundHandler(_ssrf) { InnerHandler = inner });

    // ── Redirects controlados ────────────────────────────────────────

    [Fact]
    public async Task Redirect_302_hacia_metadata_no_se_sigue()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(
            Redirect(HttpStatusCode.Found, "http://169.254.169.254/latest/meta-data/"),
            Ok());
        using var client = CreateClient(inner);

        var act = () => client.GetAsync("https://public.example.com/start");
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*SSRF bloqueado en redirect*");

        // Solo se hizo la request inicial: el destino privado nunca se contactó.
        inner.Requests.Should().ContainSingle()
            .Which.RequestUri!.Host.Should().Be("public.example.com");
    }

    [Fact]
    public async Task Redirect_hacia_publico_permitido_se_sigue_y_devuelve_200()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        _dns.GetAddresses("other.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(
            Redirect(HttpStatusCode.Found, "https://other.example.com/final"),
            Ok());
        using var client = CreateClient(inner);

        var response = await client.GetAsync("https://public.example.com/start");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Requests.Should().HaveCount(2);
        inner.Requests[1].RequestUri.Should().Be(new Uri("https://other.example.com/final"));
    }

    [Fact]
    public async Task Redirect_con_Location_relativa_se_resuelve_contra_uri_previa()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(
            Redirect(HttpStatusCode.Found, "/moved/here"),
            Ok());
        using var client = CreateClient(inner);

        var response = await client.GetAsync("https://public.example.com/start");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Requests[1].RequestUri.Should().Be(new Uri("https://public.example.com/moved/here"));
    }

    [Fact]
    public async Task Cadena_de_redirects_que_excede_max_hops_se_rechaza()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(
            Redirect(HttpStatusCode.Found, "https://public.example.com/1"),
            Redirect(HttpStatusCode.Found, "https://public.example.com/2"),
            Redirect(HttpStatusCode.Found, "https://public.example.com/3"),
            Redirect(HttpStatusCode.Found, "https://public.example.com/4"),
            Ok());
        using var client = CreateClient(inner);

        var act = () => client.GetAsync("https://public.example.com/start");
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage($"*excede el máximo de {SsrfOutboundHandler.MaxRedirectHops}*");

        // Inicial + MaxRedirectHops seguidos; el 4º redirect ya no se sigue.
        inner.Requests.Should().HaveCount(1 + SsrfOutboundHandler.MaxRedirectHops);
    }

    [Fact]
    public async Task Redirect_3xx_sin_Location_se_devuelve_sin_seguir()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.Found));
        using var client = CreateClient(inner);

        var response = await client.GetAsync("https://public.example.com/start");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        inner.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Redirect_a_otro_host_no_propaga_Authorization()
    {
        _dns.GetAddresses("public.example.com").Returns(new[] { PublicIp });
        _dns.GetAddresses("other.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(
            Redirect(HttpStatusCode.Found, "https://other.example.com/final"),
            Ok());
        using var client = CreateClient(inner);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://public.example.com/start");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secreto");
        await client.SendAsync(request);

        inner.Requests[1].Headers.Contains("Authorization").Should().BeFalse();
    }

    // ── Pin de IP anti DNS rebinding ─────────────────────────────────

    [Fact]
    public void Primary_handler_endurecido_tiene_AllowAutoRedirect_false_y_ConnectCallback()
    {
        using var handler = SsrfHttpHandlerFactory.Create(_ssrf);

        handler.AllowAutoRedirect.Should().BeFalse();
        handler.ConnectCallback.Should().NotBeNull();
    }

    [Fact]
    public void Rebinding_dns_que_cambia_a_privada_entre_validate_y_connect_no_alcanza_la_privada()
    {
        // 1ª resolución (validación de URI): pública. 2ª (connect): privada → rebinding.
        _dns.GetAddresses("rebind.example.com").Returns(new[] { PublicIp }, new[] { PrivateIp });

        _ssrf.ValidateOutboundUri("https://rebind.example.com/").IsSuccess.Should().BeTrue();

        // El pin revalida la IP en el momento de conectar: la privada se bloquea,
        // por lo que la conexión nunca llega a 10.1.2.3.
        var act = () => SsrfHttpHandlerFactory.ResolvePinnedEndpoint(
            _ssrf, new DnsEndPoint("rebind.example.com", 443));
        act.Should().Throw<HttpRequestException>().WithMessage("*pin de IP*");
    }

    [Fact]
    public void Pin_de_IP_conecta_a_la_direccion_validada()
    {
        _dns.GetAddresses("stable.example.com").Returns(new[] { PublicIp });

        var endpoint = SsrfHttpHandlerFactory.ResolvePinnedEndpoint(
            _ssrf, new DnsEndPoint("stable.example.com", 443));

        endpoint.Address.Should().Be(PublicIp);
        endpoint.Port.Should().Be(443);
        // El pin resuelve UNA sola vez y conecta a esa misma IP validada.
        _dns.Received(1).GetAddresses("stable.example.com");
    }

    [Fact]
    public void ResolvePinnedAddress_bloquea_ip_literal_privada()
    {
        var result = _ssrf.ResolvePinnedAddress("169.254.169.254");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no permitida");
    }

    // ── IntegrationConnectionTester con named client SSRF ────────────

    [Theory]
    [InlineData("https://10.0.0.5:9000")]
    [InlineData("http://169.254.169.254/")]
    [InlineData("https://metadata.google.internal/")]
    public async Task Tester_rechaza_destino_privado_con_el_mismo_criterio_que_el_dispatcher(string baseUrl)
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        var tester = new IntegrationConnectionTester(
            httpFactory, _ssrf, NullLogger<IntegrationConnectionTester>.Instance);

        var ok = await tester.TestConnectionAsync(IntegrationType.SonarQube, baseUrl, "token");

        ok.Should().BeFalse();
        httpFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task Tester_usa_el_named_client_con_handler_ssrf()
    {
        _dns.GetAddresses("sonar.example.com").Returns(new[] { PublicIp });
        var inner = new ScriptedHandler(Ok());
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("notifications").Returns(CreateClient(inner));

        var tester = new IntegrationConnectionTester(
            httpFactory, _ssrf, NullLogger<IntegrationConnectionTester>.Instance);

        var ok = await tester.TestConnectionAsync(
            IntegrationType.SonarQube, "https://sonar.example.com", "token");

        ok.Should().BeTrue();
        httpFactory.Received(1).CreateClient("notifications");
        inner.Requests.Should().ContainSingle()
            .Which.RequestUri.Should().Be(new Uri("https://sonar.example.com/api/ce/info"));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private static HttpResponseMessage Redirect(HttpStatusCode status, string location)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    /// <summary>Devuelve respuestas en orden por cada request recibida y las registra.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public ScriptedHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
