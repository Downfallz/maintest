using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Shared.Primitives;

// Bus (unifié)
public interface IEventBus
{
    Task PublishAsync(IEvent evt, CancellationToken ct = default);
}