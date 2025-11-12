namespace DA.Game.Shared;
public interface IEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}