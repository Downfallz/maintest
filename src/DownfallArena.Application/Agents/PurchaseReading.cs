namespace DownfallArena.Application.Agents;

/// <summary>
/// How far the lookahead reads a purchase (ADR 0094): how many rounds each rollout plays from the round of
/// the purchase, and how many rollouts, each on its own dice, a candidate is averaged over.
/// </summary>
public sealed record PurchaseReading(int Rounds, int Rollouts)
{
    public static PurchaseReading Default { get; } = new(4, 4);
}
