using FluentAssertions;
using QAGuardian.Application.Features.Notifications;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;
using Xunit;

namespace QAGuardian.UnitTests.Application;

/// <summary>B7: GET/mapeo de canales no debe exponer Target (webhooks, botToken|chatId) en claro.</summary>
public class NotificationChannelDtoMaskingTests
{
    [Fact]
    public void MaskTarget_no_expone_webhook_completo()
    {
        const string secret =
            "https://hooks.slack.com/services/T00/B00/super-secret-token-xyz9";

        var hint = NotificationChannelDto.MaskTarget(secret);

        hint.Should().NotBeNullOrWhiteSpace();
        hint.Should().NotContain("hooks.slack.com");
        hint.Should().NotContain("super-secret-token");
        hint.Should().Be("***xyz9");
        hint.Should().NotBe(secret);
    }

    [Fact]
    public void MaskTarget_no_expone_bot_token_de_telegram()
    {
        const string secret = "123456:ABC-DEF_secret_bot_token|987654321";

        var hint = NotificationChannelDto.MaskTarget(secret);

        hint.Should().Be("***4321");
        hint.Should().NotContain("ABC-DEF");
        hint.Should().NotContain("secret_bot");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskTarget_devuelve_null_si_no_hay_destino(string? target)
    {
        NotificationChannelDto.MaskTarget(target).Should().BeNull();
    }

    [Fact]
    public void MaskTarget_oculta_destinos_muy_cortos()
    {
        NotificationChannelDto.MaskTarget("ab").Should().Be("****");
        NotificationChannelDto.MaskTarget("abcd").Should().Be("****");
    }

    [Fact]
    public void FromEntity_marca_configured_y_omite_Target_completo()
    {
        var entity = new NotificationChannelConfig(
            null,
            NotificationChannel.Slack,
            "https://hooks.slack.com/services/T00/B00/super-secret-token-xyz9",
            NotificationEvents.TestFailed);

        var dto = NotificationChannelDto.FromEntity(entity);

        dto.IsConfigured.Should().BeTrue();
        dto.TargetHint.Should().Be("***xyz9");
        dto.TargetHint.Should().NotBe(entity.Target);
        // El record no tiene propiedad Target: el contrato JSON ya no la serializa.
        typeof(NotificationChannelDto).GetProperty("Target").Should().BeNull();
    }
}
