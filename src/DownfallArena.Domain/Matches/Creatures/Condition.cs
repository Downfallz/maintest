using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// A lasting effect attached to a creature, counting down its remaining rounds.
/// </summary>
public sealed class Condition
{
    internal Condition(LastingEffect effect)
    {
        Effect = effect;
        RemainingRounds = effect.Duration.Rounds;
    }

    public LastingEffect Effect { get; }

    /// <summary>
    /// Rounds left before the condition expires; <c>null</c> when permanent.
    /// </summary>
    public int? RemainingRounds { get; private set; }

    public bool IsPermanent => RemainingRounds is null;

    public bool IsExpired => RemainingRounds == 0;

    public ConditionSnapshot Snapshot() => new(Effect, RemainingRounds);

    internal void Refresh() => RemainingRounds = Effect.Duration.Rounds;

    internal void Tick()
    {
        if (RemainingRounds is > 0)
        {
            RemainingRounds--;
        }
    }
}
