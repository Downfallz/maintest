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
    /// Countdowns left before the condition expires, not counting the first one after application;
    /// <c>null</c> when permanent. A one-round condition applied in combat therefore reports one remaining
    /// round through the cleanup of that round and expires at the cleanup of the next.
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
