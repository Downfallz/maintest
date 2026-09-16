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

    internal Condition(LastingEffect effect, ConditionSource? source)
    {
        Effect = effect;
        Source = source;
        RemainingRounds = effect.Duration.Rounds;
    }

    private Condition(ConditionSnapshot snapshot)
    {
        Effect = snapshot.Effect;
        Source = snapshot.Source;
        RemainingRounds = snapshot.RemainingRounds;
        _fresh = snapshot.IsFresh;
    }

    public LastingEffect Effect { get; }

    /// <summary>
    /// The cast this condition came from, so what it does at upkeep is counted against that spell and that
    /// side (ADR 0027). Null when something other than a cast applied it, which nothing does today.
    /// </summary>
    public ConditionSource? Source { get; private set; }

    /// <summary>
    /// Countdowns left before the condition expires, not counting the first one after application;
    /// <c>null</c> when permanent. A one-round condition applied in combat therefore reports one remaining
    /// round through the cleanup of that round and expires at the cleanup of the next.
    /// </summary>
    public int? RemainingRounds { get; private set; }

    public bool IsPermanent => RemainingRounds is null;

    public bool IsExpired => RemainingRounds == 0;

    public ConditionSnapshot Snapshot() => new(Effect, RemainingRounds, Source, _fresh);

    /// <summary>
    /// The condition a snapshot was taken of, at the same point of its countdown: it expires at the cleanup the
    /// original would. Only <see cref="Creature.Restore"/> builds a creature this way, for the hypothetical
    /// board of ADR 0047; a condition a match plays is applied, never restored. A snapshot is a copy of a
    /// condition a match carried, and a match never carries an expired one, a countdown past the duration, a
    /// permanent effect with a countdown, or a fresh condition below its full duration; a caller that hands
    /// one in has a bug, not a rule violation.
    /// </summary>
    internal static Condition Restore(ConditionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var duration = snapshot.Effect.Duration;
        var impossible = snapshot.RemainingRounds switch
        {
            null => !duration.IsPermanent,
            0 => true,
            var remaining => duration.IsPermanent || remaining > duration.Rounds || (snapshot.IsFresh && remaining != duration.Rounds),
        };
        if (impossible)
        {
            throw new ArgumentException($"No creature a match plays carries a {snapshot.Effect.GetType().Name} in the state this snapshot describes.", nameof(snapshot));
        }

        return new Condition(snapshot);
    }

    /// <summary>
    /// Restarts the duration, and hands the condition to the spell that did it: refreshing is what decides
    /// how long the condition still runs, so what happens from here is that cast's doing (ADR 0027).
    /// </summary>
    internal void Refresh(ConditionSource? source)
    {
        RemainingRounds = Effect.Duration.Rounds;
        Source = source;
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
