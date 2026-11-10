namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Where a journal ends: what an append is conditional on.
/// </summary>
/// <param name="Lines">How many lines the journal holds, decisions or not.</param>
/// <param name="Mark">The store's own measure of the end, such as a length in bytes. Two readings with the same mark read the same journal.</param>
public sealed record JournalPosition(long Lines, long Mark)
{
    /// <summary>The end of a journal nobody has written to.</summary>
    public static JournalPosition Start { get; } = new(0, 0);
}
