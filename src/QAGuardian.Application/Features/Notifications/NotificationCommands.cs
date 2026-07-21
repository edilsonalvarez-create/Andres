using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Notifications;

public record NotificationChannelDto(
    Guid Id,
    Guid? ProjectId,
    NotificationChannel Channel,
    /// <summary>Últimos caracteres del destino (nunca el secreto completo).</summary>
    string? TargetHint,
    bool IsConfigured,
    NotificationEvents Events,
    bool IsEnabled)
{
    private const int HintSuffixChars = 4;

    /// <summary>Proyecta la entidad sin exponer Target en claro (B7 / OWASP A01:2025).</summary>
    public static NotificationChannelDto FromEntity(NotificationChannelConfig config)
        => new(
            config.Id,
            config.ProjectId,
            config.Channel,
            MaskTarget(config.Target),
            IsConfigured: !string.IsNullOrWhiteSpace(config.Target),
            config.Events,
            config.IsEnabled);

    /// <summary>
    /// Enmascara el destino: solo un hint de los últimos caracteres (o "****" si es muy corto).
    /// Nunca devuelve webhooks, bot tokens ni correos completos.
    /// </summary>
    public static string? MaskTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        var value = target.Trim();
        if (value.Length <= HintSuffixChars)
            return "****";

        return "***" + value[^HintSuffixChars..];
    }
}

public record UpsertNotificationChannelCommand(
    Guid? ProjectId, NotificationChannel Channel, string Target,
    NotificationEvents Events, bool IsEnabled) : IRequest<Result<NotificationChannelDto>>;

public class UpsertNotificationChannelCommandValidator : AbstractValidator<UpsertNotificationChannelCommand>
{
    public UpsertNotificationChannelCommandValidator(ISsrfGuard ssrf)
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Target).NotEmpty().MaximumLength(500);
        When(x => x.Channel is NotificationChannel.MicrosoftTeams
            or NotificationChannel.Slack
            or NotificationChannel.Discord, () =>
            RuleFor(x => x.Target).Custom((target, ctx) =>
            {
                var check = ssrf.ValidateOutboundUri(target);
                if (!check.IsSuccess)
                    ctx.AddFailure(check.Error ?? "El webhook fue rechazado por política SSRF.");
            }));
        When(x => x.Channel == NotificationChannel.Email, () =>
            RuleFor(x => x.Target).EmailAddress());
    }
}

public class UpsertNotificationChannelCommandHandler
    : IRequestHandler<UpsertNotificationChannelCommand, Result<NotificationChannelDto>>
{
    private readonly IRepository<NotificationChannelConfig> _channels;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public UpsertNotificationChannelCommandHandler(
        IRepository<NotificationChannelConfig> channels,
        IProjectAccessService access,
        ICurrentUserService currentUser,
        IUnitOfWork uow)
    {
        _channels = channels;
        _access = access;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<NotificationChannelDto>> Handle(
        UpsertNotificationChannelCommand request, CancellationToken ct)
    {
        if (request.ProjectId is Guid projectId)
            await _access.EnsureCanAccessProjectAsync(projectId, ct);
        else if (!_currentUser.IsInRole(SystemRoles.Administrator))
            return Result<NotificationChannelDto>.Failure(
                "Solo el administrador puede configurar canales globales.");

        var existing = (await _channels.ListAsync(
            c => c.ProjectId == request.ProjectId && c.Channel == request.Channel && !c.IsDeleted, ct))
            .FirstOrDefault();

        NotificationChannelConfig config;
        if (existing is not null)
        {
            existing.Update(request.Target, request.Events, request.IsEnabled);
            config = existing;
        }
        else
        {
            config = new NotificationChannelConfig(request.ProjectId, request.Channel,
                request.Target, request.Events);
            await _channels.AddAsync(config, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<NotificationChannelDto>.Success(NotificationChannelDto.FromEntity(config));
    }
}

public record GetNotificationChannelsQuery(Guid? ProjectId) : IRequest<IReadOnlyList<NotificationChannelDto>>;

public class GetNotificationChannelsQueryHandler
    : IRequestHandler<GetNotificationChannelsQuery, IReadOnlyList<NotificationChannelDto>>
{
    private readonly IRepository<NotificationChannelConfig> _channels;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationChannelsQueryHandler(
        IRepository<NotificationChannelConfig> channels,
        IProjectAccessService access,
        ICurrentUserService currentUser)
    {
        _channels = channels;
        _access = access;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<NotificationChannelDto>> Handle(
        GetNotificationChannelsQuery request, CancellationToken ct)
    {
        if (request.ProjectId is Guid projectId)
            await _access.EnsureCanAccessProjectAsync(projectId, ct);

        var accessible = (await _access.ListAccessibleProjectIdsAsync(ct)).ToHashSet();
        var isAdmin = _currentUser.IsInRole(SystemRoles.Administrator);

        var items = await _channels.ListAsync(c =>
            !c.IsDeleted
            && (request.ProjectId == null
                ? (c.ProjectId == null && isAdmin)
                  || (c.ProjectId != null && accessible.Contains(c.ProjectId.Value))
                : c.ProjectId == request.ProjectId || (c.ProjectId == null && isAdmin)),
            ct);
        return items.Select(NotificationChannelDto.FromEntity).ToList();
    }
}
