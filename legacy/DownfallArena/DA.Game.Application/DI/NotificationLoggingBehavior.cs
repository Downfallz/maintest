using MediatR;
using Microsoft.Extensions.Logging;

namespace DA.Game.Application.DI;

public sealed class NotificationLoggingBehavior<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private readonly ILogger<NotificationLoggingBehavior<TNotification>> _logger;

    public NotificationLoggingBehavior(ILogger<NotificationLoggingBehavior<TNotification>> logger)
    {
        _logger = logger;
    }

    public Task Handle(TNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("📢 Domain Event: {EventName} -> {@Event}",
            typeof(TNotification).Name, notification);
        return Task.CompletedTask;
    }
}
