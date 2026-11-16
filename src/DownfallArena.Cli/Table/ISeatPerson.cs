namespace DownfallArena.Cli.Table;

/// <summary>
/// The person a seat is kept for, as the host sees them whatever plays the match: whether they have reached
/// their seat (ADR 0092), and what they are being asked. A seat with none is played by a bot throughout.
/// </summary>
internal interface ISeatPerson
{
    /// <summary>Whether the person has reached their seat: a request carrying its token has arrived.</summary>
    bool HasArrived { get; }

    void Arrive();

    void Depart();

    /// <summary>What the person is being asked, or <c>null</c> when the match is asking them nothing.</summary>
    Asking? Waiting { get; }

    /// <summary>
    /// Raised as the match asks the person something, with the round it belongs to when the question has one:
    /// what moves a table's version (ADR 0119) and what a notice to a phone nobody is looking at is sent about
    /// (ADR 0118).
    /// </summary>
    event Action<Asking, int?>? Asked;
}
