namespace DA.Game.Shared.Utilities;
public abstract record EventBase(DateTime OccurredAt) : IEvent {
    public Guid EventId { get; } = Guid.NewGuid();
}