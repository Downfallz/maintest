using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Agents;

/// <summary>
/// What rounds played out are worth to a seat: the match they end first (one for a win, minus one for a loss,
/// zero for a draw or no end yet), then the scorer's sum over their actions. Compared in that order and never
/// added, for the reason the lookahead's round value gives (ADR 0047): no bound in a weights file's unit can
/// be trusted to dominate a win. Averaged over rollouts, the outcome is the share of wins less the share of
/// losses.
/// </summary>
public readonly record struct RolloutValue(double Outcome, double Score) : IComparable<RolloutValue>
{
    public static RolloutValue Lowest => new(double.NegativeInfinity, double.NegativeInfinity);

    public static RolloutValue Of(MatchOutcome outcome, PlayerSlot seat, double score) =>
        outcome.Winner switch
        {
            null => new RolloutValue(0, score),
            var winner when winner == seat => new RolloutValue(1, score),
            _ => new RolloutValue(-1, score),
        };

    public static RolloutValue Mean(IReadOnlyList<RolloutValue> values) =>
        new(values.Average(value => value.Outcome), values.Average(value => value.Score));

    public int CompareTo(RolloutValue other)
    {
        var outcome = Outcome.CompareTo(other.Outcome);
        return outcome != 0 ? outcome : Score.CompareTo(other.Score);
    }

    public static bool operator <(RolloutValue left, RolloutValue right) => left.CompareTo(right) < 0;

    public static bool operator <=(RolloutValue left, RolloutValue right) => left.CompareTo(right) <= 0;

    public static bool operator >(RolloutValue left, RolloutValue right) => left.CompareTo(right) > 0;

    public static bool operator >=(RolloutValue left, RolloutValue right) => left.CompareTo(right) >= 0;
}
