namespace DownfallArena.Cli.Table;

/// <summary>What became of a reaction a seat sent (ADR 0173): sent, or why not.</summary>
internal sealed record ReactionSent(TableReaction? Reaction, string? Refusal, TimeSpan Wait)
{
    public static ReactionSent Unknown { get; } = new(null, "Reaction.Unknown", TimeSpan.Zero);

    /// <summary>Another process kept writing the table's reactions first, every time this one tried.</summary>
    public static ReactionSent Busy { get; } = new(null, "Reaction.Busy", TimeSpan.Zero);

    public static ReactionSent Of(TableReaction reaction) => new(reaction, null, TimeSpan.Zero);

    public static ReactionSent TooSoon(TimeSpan wait) => new(null, "Reaction.TooSoon", wait);
}
