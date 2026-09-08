namespace DA.Game.Application.Shared.Messaging;

public interface IApplicationEventCollector
{
    void Add(IApplicationEvent appEvent);
    IReadOnlyCollection<IApplicationEvent> DequeueAll();
}