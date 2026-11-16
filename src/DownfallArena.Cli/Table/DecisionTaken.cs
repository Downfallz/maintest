namespace DownfallArena.Cli.Table;

/// <summary>
/// What became of a decision handed to a session.
/// </summary>
/// <param name="Taken">False when the seat had moved on: the tap is late, not illegal.</param>
/// <param name="SetsOff">
/// Whether taking it can have set a command off, which is when the trace is worth keeping again. One of a
/// seat's picks, speeds or intents does not until its last, and the last does not while the other person is
/// still being asked theirs: the answers reach the match together (ADR 0111, ADR 0117).
/// </param>
internal sealed record DecisionTaken(bool Taken, bool SetsOff)
{
    public static DecisionTaken Late { get; } = new(false, false);
}
