using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// The players who let the table's time to answer run out (ADR 0169). Like a concession it ends the match and is no
/// decision the options offer, so it is a line of its own: a match rebuilt from its journal times out where it did.
/// </summary>
public sealed record TimeoutEntry(IReadOnlyList<PlayerSlot> Late) : IJournalEntry
{
    public bool Equals(TimeoutEntry? other) => other is not null && Late.SequenceEqual(other.Late);

    public override int GetHashCode() => Late.Aggregate(17, (hash, slot) => (hash * 31) + slot.GetHashCode());
}
