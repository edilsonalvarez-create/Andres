using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Notifications;

public record NotificationChannelDto(Guid Id, Guid? ProjectId, NotificationChannel Channel,
    string Target, NotificationEvents Events, bool IsEnabled);

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
        return Result<NotificationChannelDto>.Success(new NotificationChannelDto(
            config.Id, config.ProjectId, config.Channel, config.Target, config.Events, config.IsEnabled));
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
        return items.Select(c => new NotificationChannelDto(
            c.Id, c.ProjectId, c.Channel, c.Target, c.Events, c.IsEnabled)).ToList();
    }
}
