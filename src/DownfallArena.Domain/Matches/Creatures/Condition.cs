using DownfallArena.Domain.Resources.Effects;

namespace DownfallArena.Domain.Matches.Creatures;

/// <summary>
/// A lasting effect attached to a creature, counting down its remaining rounds. The first tick after application
/// does not count: an effect applied during a round lasts through the following rounds, so a one-round stun
/// applied in combat stuns the creature for the whole next round.
/// </summary>
public sealed class Condition
{
    private bool _fresh = true;

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

    internal void Refresh()
    {
        RemainingRounds = Effect.Duration.Rounds;
        _fresh = true;
    }

    internal void Tick()
    {
        if (_fresh)
        {
            _fresh = false;
            return;
        }

        if (RemainingRounds is > 0)
        {
            RemainingRounds--;
        }
    }
}
