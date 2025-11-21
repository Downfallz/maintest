using DA.Game.Domain2.Shared.Messaging;

namespace DA.Game.Application.Shared.Primitives;

public static class EventPublishingExtensions
{
    public static async Task PublishDomainEventsAsync(
        this IEventBus bus, IHasDomainEvents aggregate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(bus);

        foreach (var e in aggregate.DequeueEvents())
        {
            await bus.PublishAsync(e, ct);
        }

    }
}