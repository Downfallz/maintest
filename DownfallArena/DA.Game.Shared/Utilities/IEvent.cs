namespace DA.Game.Shared.Utilities;
public interface IEvent {
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}