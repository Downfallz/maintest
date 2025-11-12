using DA.Game.Shared;

namespace DA.Game.Application.Shared.Abstractions;

// Bus (unifié)
public interface IEventBus
{
    Task PublishAsync(IEvent evt, CancellationToken ct = default);
}