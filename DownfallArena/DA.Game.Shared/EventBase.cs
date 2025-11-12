namespace DA.Game.Shared;
public abstract record EventBase(DateTime OccurredAt) : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}