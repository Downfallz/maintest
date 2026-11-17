namespace DownfallArena.Cli.Table;

/// <summary>What a page puts to say what its seat was shown (ADR 0194): the last feed sequence it showed.</summary>
internal sealed record TableSeenBody(long? Sequence);
