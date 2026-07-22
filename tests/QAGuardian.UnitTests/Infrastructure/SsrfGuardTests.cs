using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Infrastructure.Security;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class SsrfGuardTests
{
    private readonly IHostAddressResolver _dns = Substitute.For<IHostAddressResolver>();

    private SsrfGuard CreateGuard(Dictionary<string, string?>? overrides = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["Security:Ssrf:AllowHttpHosts:0"] = "",
            ["Security:Ssrf:AllowPrivateHosts:0"] = "",
        };
        if (overrides is not null)
        {
            foreach (var (k, v) in overrides)
                data[k] = v;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(data!).Build();
        return new SsrfGuard(_dns, config, NullLogger<SsrfGuard>.Instance);
    }

    private SsrfGuard CreateDevAllowlistGuard()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Ssrf:AllowHttpHosts:0"] = "localhost",
            ["Security:Ssrf:AllowHttpHosts:1"] = "127.0.0.1",
        }).Build();
        return new SsrfGuard(_dns, config, NullLogger<SsrfGuard>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    public void Rejects_invalid_or_empty_uri(string? uri)
    {
        var result = CreateGuard().ValidateOutboundUri(uri);
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://example.com/hook")]
    [InlineData("ftp://example.com/file")]
    [InlineData("gopher://example.com/")]
    public void Rejects_non_https_without_allowlist(string uri)
    {
        _dns.GetAddresses("example.com").Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var result = CreateGuard().ValidateOutboundUri(uri);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Match(e =>
            e!.Contains("HTTPS", StringComparison.OrdinalIgnoreCase)
            || e.Contains("permitido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_file_scheme()
    {
        var result = CreateGuard().ValidateOutboundUri("file:///etc/passwd");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Allows_https_public_host_after_dns()
    {
        _dns.GetAddresses("hooks.example.com").Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var result = CreateGuard().ValidateOutboundUri("https://hooks.example.com/webhook");
        result.IsSuccess.Should().BeTrue();
        result.Value!.Host.Should().Be("hooks.example.com");
    }

    [Fact]
    public void Dev_allowlist_permits_http_localhost()
    {
        _dns.GetAddresses("localhost").Returns(new[] { IPAddress.Loopback });
        var result = CreateDevAllowlistGuard().ValidateOutboundUri("http://localhost:7071/api/hook");
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Theory]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://127.0.0.1:8443/x")]
    [InlineData("https://10.0.0.5/x")]
    [InlineData("https://10.255.255.255/x")]
    [InlineData("https://172.16.0.1/x")]
    [InlineData("https://172.31.255.1/x")]
    [InlineData("https://192.168.1.10/x")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://169.254.1.1/x")]
    [InlineData("https://0.0.0.0/x")]
    [InlineData("https://100.64.0.1/x")]
    public void Blocks_private_loopback_linklocal_literal_ipv4(string uri)
    {
        var result = CreateGuard().ValidateOutboundUri(uri);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no permitida");
    }

    [Theory]
    [InlineData("https://[::1]/")]
    [InlineData("https://[fe80::1]/")]
    [InlineData("https://[fc00::1]/")]
    [InlineData("https://[fd12:3456:789a::1]/")]
    [InlineData("https://[ff02::1]/")]
    public void Blocks_ipv6_loopback_linklocal_ula_multicast(string uri)
    {
        var result = CreateGuard().ValidateOutboundUri(uri);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Blocks_ipv4_mapped_ipv6_loopback()
    {
        var result = CreateGuard().ValidateOutboundUri("https://[::ffff:127.0.0.1]/");
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://metadata.google.internal/")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("https://METADATA.GOOGLE.INTERNAL/")]
    public void Blocks_cloud_metadata_hostnames(string uri)
    {
        var result = CreateGuard().ValidateOutboundUri(uri);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("metadata");
    }

    [Fact]
    public void Blocks_when_dns_resolves_to_rfc1918()
    {
        _dns.GetAddresses("evil.example.com").Returns(new[] { IPAddress.Parse("10.1.2.3") });
        var result = CreateGuard().ValidateOutboundUri("https://evil.example.com/ssrf");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("10.1.2.3");
    }

    [Fact]
    public void Blocks_when_dns_resolves_to_metadata_ip()
    {
        _dns.GetAddresses("attacker.test").Returns(new[] { IPAddress.Parse("169.254.169.254") });
        var result = CreateGuard().ValidateOutboundUri("https://attacker.test/");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Blocks_when_any_resolved_address_is_private()
    {
        _dns.GetAddresses("mixed.example.com").Returns(new[]
        {
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("192.168.0.2"),
        });
        var result = CreateGuard().ValidateOutboundUri("https://mixed.example.com/");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Blocks_userinfo_credentials_in_uri()
    {
        _dns.GetAddresses("example.com").Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var result = CreateGuard().ValidateOutboundUri("https://user:pass@example.com/hook");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("credenciales");
    }

    [Fact]
    public void Blocks_decimal_ipv4_loopback_bypass()
    {
        // 2130706433 == 127.0.0.1
        var result = CreateGuard().ValidateOutboundUri("https://2130706433/");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_dns_throws()
    {
        _dns.GetAddresses("missing.invalid").Returns(_ => throw new SocketException());
        var result = CreateGuard().ValidateOutboundUri("https://missing.invalid/");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("resolver");
    }

    [Fact]
    public void Fails_when_dns_returns_empty()
    {
        _dns.GetAddresses("empty.example").Returns(Array.Empty<IPAddress>());
        var result = CreateGuard().ValidateOutboundUri("https://empty.example/");
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.5.5", true)]
    [InlineData("172.15.0.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("169.254.169.254", true)]
    [InlineData("127.0.0.1", true)]
    public void IsBlockedAddress_ipv4_matrix(string ip, bool blocked)
    {
        SsrfGuard.IsBlockedAddress(IPAddress.Parse(ip)).Should().Be(blocked);
    }

    [Theory]
    [InlineData("::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fc00::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("2001:4860:4860::8888", false)]
    public void IsBlockedAddress_ipv6_matrix(string ip, bool blocked)
    {
        SsrfGuard.IsBlockedAddress(IPAddress.Parse(ip)).Should().Be(blocked);
    }
}
