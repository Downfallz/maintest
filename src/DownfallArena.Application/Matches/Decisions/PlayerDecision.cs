using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Decisions;

/// <summary>
/// One decision a seat submits, named by the kind of <see cref="PlayerOptions" /> it answers. A host builds
/// one from what a player tapped and has <see cref="PlayerDecisionCheck" /> say whether the options offered
/// it, before the agent waiting on that seat is handed anything.
/// </summary>
public sealed record PlayerDecision
{
    private PlayerDecision(PlayerOptionsKind kind)
    {
        Kind = kind;
    }

    /// <summary>The kind of options this decision answers. It must match what the seat is being asked.</summary>
    public PlayerOptionsKind Kind { get; }

    /// <summary>The creature the decision is about; none for a pass, and none for a target binding.</summary>
    public CreatureId? Creature { get; private init; }

    /// <summary>The spell unlocked or declared; none for a speed choice, a pass, or a target binding.</summary>
    public SpellId? Spell { get; private init; }

    public Speed? Speed { get; private init; }

    /// <summary>The targets bound to the intent being revealed. Empty when the spell has no legal target.</summary>
    public IReadOnlyList<CreatureId> Targets { get; private init; } = [];

    /// <summary>Whether an evolution decision gives up the player's remaining picks for the round.</summary>
    public bool IsPass { get; private init; }

    /// <summary>Gives up the remaining evolution picks. Always legal while evolution is pending.</summary>
    public static PlayerDecision Pass { get; } = new(PlayerOptionsKind.Evolution) { IsPass = true };

    public static PlayerDecision Unlock(CreatureId creature, SpellId spell) =>
        new(PlayerOptionsKind.Evolution) { Creature = creature, Spell = spell };

    public static PlayerDecision ChooseSpeed(CreatureId creature, Speed speed) =>
        new(PlayerOptionsKind.Speed) { Creature = creature, Speed = speed };

    public static PlayerDecision DeclareIntent(CreatureId creature, SpellId spell) =>
        new(PlayerOptionsKind.Intent) { Creature = creature, Spell = spell };

    public static PlayerDecision BindTargets(IReadOnlyList<CreatureId> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return new(PlayerOptionsKind.Target) { Targets = [.. targets] };
    }

    public bool Equals(PlayerDecision? other) =>
        other is not null
        && Kind == other.Kind
        && Creature == other.Creature
        && Spell == other.Spell
        && Speed == other.Speed
        && IsPass == other.IsPass
        && Targets.SequenceEqual(other.Targets);

    public override int GetHashCode() => HashCode.Combine(Kind, Creature, Spell, Speed, IsPass, Targets.Count);
}
