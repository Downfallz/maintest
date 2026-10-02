namespace DownfallArena.Application.Agents;

/// <summary>
/// How far the lookahead reads a purchase (ADR 0094): how many rounds each rollout plays from the round of
/// the purchase, and how many rollouts, each on its own dice, a candidate is averaged over. Both at least one:
/// no round reads nothing, and no rollout has no mean.
/// </summary>
public sealed record PurchaseReading
{
    public PurchaseReading(int rounds, int rollouts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rounds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rollouts, 1);
        Rounds = rounds;
        Rollouts = rollouts;
    }

    public static PurchaseReading Default { get; } = new(4, 4);

    public int Rounds { get; }

    public int Rollouts { get; }
}
