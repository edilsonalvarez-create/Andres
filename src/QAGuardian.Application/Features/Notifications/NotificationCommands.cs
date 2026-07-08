using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
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
    public UpsertNotificationChannelCommandValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Target).NotEmpty().MaximumLength(500);
        When(x => x.Channel != NotificationChannel.Email && x.Channel != NotificationChannel.Telegram, () =>
            RuleFor(x => x.Target).Must(t => Uri.TryCreate(t, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .WithMessage("El webhook debe ser una URL HTTPS válida."));
        When(x => x.Channel == NotificationChannel.Email, () =>
            RuleFor(x => x.Target).EmailAddress());
    }
}

public class UpsertNotificationChannelCommandHandler
    : IRequestHandler<UpsertNotificationChannelCommand, Result<NotificationChannelDto>>
{
    private readonly IRepository<NotificationChannelConfig> _channels;
    private readonly IUnitOfWork _uow;

    public UpsertNotificationChannelCommandHandler(
        IRepository<NotificationChannelConfig> channels, IUnitOfWork uow)
    {
        _channels = channels;
        _uow = uow;
    }

    public async Task<Result<NotificationChannelDto>> Handle(
        UpsertNotificationChannelCommand request, CancellationToken ct)
    {
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

    public GetNotificationChannelsQueryHandler(IRepository<NotificationChannelConfig> channels)
        => _channels = channels;

    public async Task<IReadOnlyList<NotificationChannelDto>> Handle(
        GetNotificationChannelsQuery request, CancellationToken ct)
    {
        var items = await _channels.ListAsync(
            c => !c.IsDeleted && (request.ProjectId == null || c.ProjectId == request.ProjectId || c.ProjectId == null), ct);
        return items.Select(c => new NotificationChannelDto(
            c.Id, c.ProjectId, c.Channel, c.Target, c.Events, c.IsEnabled)).ToList();
    }
}
