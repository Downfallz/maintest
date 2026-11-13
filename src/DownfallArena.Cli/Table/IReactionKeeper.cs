namespace DownfallArena.Cli.Table;

/// <summary>Where a recorded table keeps its reactions (ADR 0173): read with the tag of what was read, written only over it.</summary>
internal interface IReactionKeeper
{
    /// <summary>The reactions kept, and the tag of what was read; none and no tag before the first one.</summary>
    Task<(IReadOnlyList<TableReaction> Items, string? Tag)> ReactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes these reactions if what is kept is still what was read under <paramref name="expected" />; the new tag, or none when it moved.</summary>
    Task<string?> WriteReactionsIfAsync(IReadOnlyList<TableReaction> items, string? expected, CancellationToken cancellationToken = default);
}
