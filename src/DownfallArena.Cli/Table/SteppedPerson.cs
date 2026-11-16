namespace DownfallArena.Cli.Table;

/// <summary>
/// The person of a seat at a stepped table (ADR 0159). Nothing waits on them: whether they have arrived is all
/// this holds, and what they are being asked is read off the table's session, which says it when it changes.
/// </summary>
internal sealed class SteppedPerson : ISeatPerson
{
    private int _arrived;
    private Func<Asking?> _waiting = static () => null;

    public bool HasArrived => Volatile.Read(ref _arrived) != 0;

    public void Arrive() => Volatile.Write(ref _arrived, 1);

    public void Depart() => Volatile.Write(ref _arrived, 0);

    public Asking? Waiting => Volatile.Read(ref _waiting)();

    public event Action<Asking, int?>? Asked;

    /// <summary>Says where this person's question is read from: the session, once there is one.</summary>
    public void Attach(Func<Asking?> waiting)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        Volatile.Write(ref _waiting, waiting);
    }

    /// <summary>The session's way of saying the person has just been asked something new.</summary>
    public void Ask(Asking asking, int? round)
    {
        ArgumentNullException.ThrowIfNull(asking);
        Asked?.Invoke(asking, round);
    }
}
