using DA.Game.Domain2.Shared.Messaging;

namespace DA.Game.Application.Shared.Messaging;

public sealed class AggregateTracker : IAggregateTracker
{
    private readonly List<IHasDomainEvents> _touched = new();

    public void Track(IHasDomainEvents aggregate)
    {
        if (aggregate is null) return;
        _touched.Add(aggregate);
    }

    public IReadOnlyCollection<IHasDomainEvents> DequeueAll()
    {
        var copy = _touched.ToArray();
        _touched.Clear();
        return copy;
    }
}