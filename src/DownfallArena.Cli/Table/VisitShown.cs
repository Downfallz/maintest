namespace DownfallArena.Cli.Table;

/// <summary>What became of a seat's word on what it was shown (ADR 0194): the mark kept afterwards, or why there is none.</summary>
internal sealed record VisitShown(LastVisit? Visit, string? Refusal)
{
    /// <summary>Another process kept writing the mark first, the store failed, or the table's record could not be told apart from a deleted one.</summary>
    public static VisitShown Busy { get; } = new(null, "Seen.Busy");

    /// <summary>The operator deleted the table's run: nothing is kept for it again (#850).</summary>
    public static VisitShown Gone { get; } = new(null, "Seen.Gone");

    public static VisitShown Of(LastVisit visit) => new(visit, null);
}
