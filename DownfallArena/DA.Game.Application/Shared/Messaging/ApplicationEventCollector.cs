namespace DA.Game.Application.Shared.Messaging;
public sealed class ApplicationEventCollector : IApplicationEventCollector
{
    private readonly List<IApplicationEvent> _buffer = new();
    public void Add(IApplicationEvent appEvent) { if (appEvent != null) _buffer.Add(appEvent); }
    public IReadOnlyCollection<IApplicationEvent> DequeueAll()
    {
        var copy = _buffer.ToArray();
        _buffer.Clear();
        return copy;
    }
}