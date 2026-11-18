namespace DownfallArena.Cli.Table;

/// <summary>Where a recorded table keeps its reactions (ADR 0173): read with the tag of what was read, written only over it.</summary>
internal interface IReactionKeeper
{
    /// <summary>The reactions kept, and the tag of what was read; none and no tag before the first one.</summary>
    Task<(IReadOnlyList<TableReaction> Items, string? Tag)> ReactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes these reactions if what is kept is still what was read under <paramref name="expected" />; the new tag, or none when it moved.</summary>
    Task<string?> WriteReactionsIfAsync(IReadOnlyList<TableReaction> items, string? expected, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the table's record is still kept: gone once the operator deleted its run, which a first write would bring
    /// back (#789); unknown when this process never saw it stored, which is no evidence of a delete (#850).
    /// </summary>
    Task<RecordPresence> RecordedAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the reactions file only if it is still the one written under <paramref name="expected" />. Undoes a first write made while the run was being deleted (#818).</summary>
    Task<bool> ForgetReactionsIfAsync(string expected, CancellationToken cancellationToken = default);
}
