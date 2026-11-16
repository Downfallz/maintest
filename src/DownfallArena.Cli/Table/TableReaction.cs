namespace DownfallArena.Cli.Table;

/// <summary>
/// One reaction a seat sent (ADR 0173): which of the <see cref="TableReactions.Catalogue" /> it was, by which seat,
/// and when. <paramref name="Sequence" /> grows by one a reaction for the life of the table, so a page tells the
/// ones it has shown from the ones it has not by number alone.
/// </summary>
internal sealed record TableReaction(long Sequence, string Seat, string Reaction, DateTimeOffset At);
