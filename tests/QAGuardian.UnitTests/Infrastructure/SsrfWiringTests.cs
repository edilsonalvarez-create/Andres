using System.Net;
using System.Net.Http;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Integrations;
using QAGuardian.Application.Features.Notifications;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Clients;
using QAGuardian.Infrastructure.Notifications;
using QAGuardian.Infrastructure.Persistence;
using QAGuardian.Infrastructure.Security;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Sprint 12-C: cableado de SsrfGuard en validators, tester y dispatcher.</summary>
public class SsrfWiringTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QAGuardianDbContext _context;
    private readonly IHostAddressResolver _dns = Substitute.For<IHostAddressResolver>();
    private readonly SsrfGuard _ssrf;

    public SsrfWiringTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new QAGuardianDbContext(options);
        _context.Database.EnsureCreated();

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        _ssrf = new SsrfGuard(_dns, config, NullLogger<SsrfGuard>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData("http://169.254.169.254/")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://metadata.google.internal/")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://10.0.0.1/webhook")]
    public void NotificationValidator_rechaza_webhook_ssrf(string target)
    {
        var validator = new UpsertNotificationChannelCommandValidator(_ssrf);
        var result = validator.TestValidate(new UpsertNotificationChannelCommand(
            null, NotificationChannel.Slack, target, NotificationEvents.TestFailed, true));
        result.ShouldHaveValidationErrorFor(x => x.Target);
    }

    [Fact]
    public void NotificationValidator_acepta_https_publico()
    {
        _dns.GetAddresses("hooks.slack.com").Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var validator = new UpsertNotificationChannelCommandValidator(_ssrf);
        var result = validator.TestValidate(new UpsertNotificationChannelCommand(
            null, NotificationChannel.Slack,
            "https://hooks.slack.com/services/T/B/X",
            NotificationEvents.TestFailed, true));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("http://169.254.169.254/")]
    [InlineData("https://169.254.169.254/")]
    [InlineData("https://192.168.1.1:9000")]
    public void UpsertIntegrationValidator_rechaza_BaseUrl_Sonar_ssrf(string baseUrl)
    {
        var validator = new UpsertIntegrationCommandValidator(_ssrf);
        var result = validator.TestValidate(new UpsertIntegrationCommand(
            Guid.NewGuid(), IntegrationType.SonarQube, baseUrl, "token", null, true));
        result.ShouldHaveValidationErrorFor(x => x.BaseUrl);
    }

    [Fact]
    public void TestConnectionValidator_rechaza_metadata()
    {
        var validator = new TestIntegrationConnectionCommandValidator(_ssrf);
        var result = validator.TestValidate(new TestIntegrationConnectionCommand(
            IntegrationType.SonarQube, "http://169.254.169.254/", "t", null));
        result.ShouldHaveValidationErrorFor(x => x.BaseUrl);
    }

    [Fact]
    public async Task IntegrationConnectionTester_rechaza_Sonar_metadata_sin_HTTP()
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        var tester = new IntegrationConnectionTester(
            httpFactory, _ssrf, NullLogger<IntegrationConnectionTester>.Instance);

        var ok = await tester.TestConnectionAsync(
            IntegrationType.SonarQube, "http://169.254.169.254/", "token");

        ok.Should().BeFalse();
        httpFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task NotificationDispatcher_no_posta_webhook_metadata()
    {
        _context.NotificationChannels.Add(new NotificationChannelConfig(
            null, NotificationChannel.Discord, "http://169.254.169.254/",
            NotificationEvents.TestFailed));
        await _context.SaveChangesAsync();

        var httpFactory = Substitute.For<IHttpClientFactory>();
        var handler = new RecordingHandler();
        var client = new HttpClient(handler);
        httpFactory.CreateClient("notifications").Returns(client);

        var dispatcher = new NotificationDispatcher(
            _context, httpFactory, _ssrf,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(new NotificationMessage(
            NotificationEvents.TestFailed, "Falló", "detalle", null, null));

        handler.Requests.Should().BeEmpty();
        httpFactory.DidNotReceive().CreateClient("notifications");
    }

    [Fact]
    public async Task SsrfOutboundHandler_bloquea_metadata()
    {
        var inner = new RecordingHandler();
        var handler = new SsrfOutboundHandler(_ssrf) { InnerHandler = inner };
        using var client = new HttpClient(handler);

        var act = () => client.GetAsync("http://169.254.169.254/");
        await act.Should().ThrowAsync<HttpRequestException>();
        inner.Requests.Should().BeEmpty();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
