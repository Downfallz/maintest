namespace DownfallArena.Application.Tables.Tending;

/// <summary>What the runs of a span of time did, added up: the line over the Jobs tab's list.</summary>
public sealed record TendTotals(int Runs, int Failed, int NeverBegan, int InvitationsExpired, int TimedOut, int Abandoned, int Warned)
{
    public static readonly TendTotals None = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>The runs that started at or after <paramref name="since" />, added up.</summary>
    public static TendTotals Since(IEnumerable<TendRun> runs, DateTimeOffset since)
    {
        ArgumentNullException.ThrowIfNull(runs);
        return runs.Where(run => run.Started >= since).Aggregate(None, (total, run) => new TendTotals(
            total.Runs + 1,
            total.Failed + (run.Outcome == TendOutcome.Failed ? 1 : 0),
            total.NeverBegan + run.NeverBegan.Count,
            total.InvitationsExpired + run.InvitationsExpired,
            total.TimedOut + run.TimedOut.Count,
            total.Abandoned + run.Abandoned.Count,
            total.Warned + run.Warned.Count));
    }
}
