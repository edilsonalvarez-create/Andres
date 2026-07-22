using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Infrastructure.Security;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class SqlHostGuardTests
{
    private readonly IHostAddressResolver _dns = Substitute.For<IHostAddressResolver>();

    private SqlHostGuard CreateGuard(string environment = "Production", params string[] allowedHosts)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var options = Options.Create(new DatabaseValidationOptions { AllowedSqlHosts = allowedHosts });
        return new SqlHostGuard(_dns, options, env, NullLogger<SqlHostGuard>.Instance);
    }

    // ── Connection string inválida / sin servidor ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_connection_string(string? connectionString)
    {
        var result = CreateGuard().ValidateConnectionString(connectionString);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Rejects_malformed_connection_string_without_echoing_it()
    {
        var result = CreateGuard().ValidateConnectionString("esto;;no=es;=válido==;Password=secreto123");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotContain("secreto123");
    }

    [Fact]
    public void Rejects_connection_string_without_data_source()
    {
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Database=Master;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Data Source");
    }

    // ── Metadata / RFC1918 / loopback (allowlist configurada, host no listado) ──

    [Fact]
    public void Rejects_cloud_metadata_ip()
    {
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=169.254.169.254;Database=Master;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("169.254.169.254");
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.10")]
    [InlineData("100.64.0.1")]
    public void Rejects_rfc1918_and_cgnat_literal_ips(string ip)
    {
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString($"Server={ip},1433;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ip);
    }

    [Fact]
    public void Rejects_loopback_in_production_when_not_allowlisted()
    {
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=127.0.0.1;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Rejects_metadata_hostname()
    {
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=metadata.google.internal;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("metadata");
    }

    [Fact]
    public void Rejects_decimal_ipv4_loopback_bypass()
    {
        // 2130706433 == 127.0.0.1
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=2130706433;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Rejects_hostname_resolving_to_private_ip()
    {
        _dns.GetAddresses("evil.example.com").Returns(new[] { IPAddress.Parse("10.1.2.3") });
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=evil.example.com;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("evil.example.com");
    }

    [Fact]
    public void Rejects_when_dns_fails()
    {
        _dns.GetAddresses("missing.invalid").Returns(_ => throw new SocketException());
        var result = CreateGuard(allowedHosts: "sql.example.com")
            .ValidateConnectionString("Server=missing.invalid;Database=QA;User Id=sa;Password=x");
        result.IsSuccess.Should().BeFalse();
    }

    // ── Allowlist hit ──────────────────────────────────────────────────────

    [Fact]
    public void Allows_allowlisted_host_even_if_it_resolves_to_private_ip()
    {
        // SQL interno legítimo: la allowlist tiene prioridad sobre el criterio SSRF.
        var result = CreateGuard(allowedHosts: "sql-interno.corp.local")
            .ValidateConnectionString("Server=sql-interno.corp.local,1433;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value.Should().Be("sql-interno.corp.local");
        _dns.DidNotReceiveWithAnyArgs().GetAddresses(default!);
    }

    [Fact]
    public void Allows_allowlisted_private_ip_literal()
    {
        var result = CreateGuard(allowedHosts: "10.5.0.20")
            .ValidateConnectionString("Server=10.5.0.20;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Fact]
    public void Allowlist_match_is_case_insensitive_and_ignores_instance_and_port()
    {
        var result = CreateGuard(allowedHosts: "SQL-Interno.Corp.Local")
            .ValidateConnectionString(@"Server=tcp:sql-interno.corp.local\SQLEXPRESS,1433;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Fact]
    public void Allowlist_supports_suffix_wildcard()
    {
        var result = CreateGuard(allowedHosts: "*.db.corp.local")
            .ValidateConnectionString("Server=sql01.db.corp.local;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Fact]
    public void Wildcard_does_not_match_other_domains()
    {
        var result = CreateGuard(allowedHosts: "*.db.corp.local")
            .ValidateConnectionString("Server=10.1.2.3;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeFalse();
    }

    // ── Allowlist miss con host público → criterio SSRF (permitido) ──────

    [Fact]
    public void Allows_public_host_not_in_allowlist_per_ssrf_criteria()
    {
        _dns.GetAddresses("sql.publico.example.com").Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var result = CreateGuard(allowedHosts: "otro.example.com")
            .ValidateConnectionString("Server=sql.publico.example.com;Database=QA;User Id=app;Password=x");
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    // ── Fail-closed: allowlist vacía fuera de Development ─────────────────

    [Theory]
    [InlineData("Server=sql.publico.example.com;Database=QA;User Id=app;Password=x")]
    [InlineData("Server=localhost;Database=QA;User Id=app;Password=x")]
    [InlineData("Server=169.254.169.254;Database=QA;User Id=app;Password=x")]
    public void Empty_allowlist_outside_development_denies_everything(string connectionString)
    {
        var result = CreateGuard("Production").ValidateConnectionString(connectionString);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("AllowedSqlHosts");
    }

    // ── Development con allowlist vacía = solo localhost documentado ──────

    [Theory]
    [InlineData("Server=localhost;Database=QA;User Id=app;Password=x")]
    [InlineData("Server=127.0.0.1,1433;Database=QA;User Id=app;Password=x")]
    [InlineData(@"Server=localhost\SQLEXPRESS;Database=QA;User Id=app;Password=x")]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=QA;Integrated Security=True")]
    public void Development_with_empty_allowlist_permits_localhost(string connectionString)
    {
        var result = CreateGuard("Development").ValidateConnectionString(connectionString);
        result.IsSuccess.Should().BeTrue(because: result.Error);
    }

    [Theory]
    [InlineData("Server=10.0.0.5;Database=QA;User Id=app;Password=x")]
    [InlineData("Server=169.254.169.254;Database=QA;User Id=app;Password=x")]
    [InlineData("Server=sql.publico.example.com;Database=QA;User Id=app;Password=x")]
    public void Development_with_empty_allowlist_rejects_non_localhost(string connectionString)
    {
        var result = CreateGuard("Development").ValidateConnectionString(connectionString);
        result.IsSuccess.Should().BeFalse();
    }

    // ── Extracción de host del DataSource ──────────────────────────────────

    [Theory]
    [InlineData("localhost", "localhost")]
    [InlineData("localhost,1433", "localhost")]
    [InlineData(@"localhost\SQLEXPRESS", "localhost")]
    [InlineData(@"localhost\SQLEXPRESS,1433", "localhost")]
    [InlineData("tcp:db.example.com,1433", "db.example.com")]
    [InlineData(@"np:\\server01\pipe\sql\query", "server01")]
    [InlineData("[::1],1433", "::1")]
    [InlineData(@"(localdb)\MSSQLLocalDB", "(localdb)")]
    public void ExtractHost_strips_protocol_instance_and_port(string dataSource, string expected)
    {
        SqlHostGuard.ExtractHost(dataSource).Should().Be(expected);
    }
}
