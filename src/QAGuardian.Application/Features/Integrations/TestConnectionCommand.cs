using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Integrations;

/// <summary>Prueba la conexión a una integración externa antes de guardar sus credenciales.
/// Espeja el shape de <see cref="UpsertIntegrationCommand"/> (Token separado, ExtraJson como string).</summary>
public sealed record TestIntegrationConnectionCommand(
    IntegrationType Type,
    string BaseUrl,
    string? Token,
    string? ExtraJson
) : IRequest<Result<string>>;

public sealed class TestIntegrationConnectionCommandValidator
    : AbstractValidator<TestIntegrationConnectionCommand>
{
    public TestIntegrationConnectionCommandValidator(ISsrfGuard ssrf)
    {
        RuleFor(x => x.Type).IsInEnum();
        When(x => x.Type is IntegrationType.SonarQube or IntegrationType.OwaspZap, () =>
            RuleFor(x => x.BaseUrl).Custom((url, ctx) =>
            {
                var check = ssrf.ValidateOutboundUri(url);
                if (!check.IsSuccess)
                    ctx.AddFailure(check.Error ?? "La URL base fue rechazada por política SSRF.");
            }));
    }
}

public sealed class TestIntegrationConnectionCommandHandler(IIntegrationConnectionTester tester)
    : IRequestHandler<TestIntegrationConnectionCommand, Result<string>>
{
    public async Task<Result<string>> Handle(TestIntegrationConnectionCommand request, CancellationToken ct)
    {
        var success = await tester.TestConnectionAsync(request.Type, request.BaseUrl, request.Token, ct);
        return success
            ? Result<string>.Success("Conexión exitosa")
            : Result<string>.Failure("No se pudo establecer la conexión con las credenciales indicadas.");
    }
}
