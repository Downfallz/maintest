using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A Spell bound to its targets at its Activation slot: a revealed Intent's Spell, or one chosen at the slot when
/// the rule set declares no Intent (ADR 0110).
/// </summary>
public sealed record CombatAction
{
    private CombatAction(CreatureId actor, SpellId spell, IReadOnlyList<CreatureId> targets)
    {
        Actor = actor;
        Spell = spell;
        Targets = targets;
    }

    public CreatureId Actor { get; }

    public SpellId Spell { get; }

    public IReadOnlyList<CreatureId> Targets { get; }

    public static CombatAction Bind(CombatIntent intent, IReadOnlyList<CreatureId> targets)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(targets);
        return new CombatAction(intent.Actor, intent.Spell, [.. targets]);
    }

    /// <summary>
    /// An action whose Spell is chosen at the slot together with its targets, with no Intent declared before
    /// (<see cref="CombatDeclaration.AtActivation" />, ADR 0110).
    /// </summary>
    public static CombatAction Choose(CreatureId actor, SpellId spell, IReadOnlyList<CreatureId> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return new CombatAction(actor, spell, [.. targets]);
    }

    public bool Equals(CombatAction? other) =>
        other is not null && Actor == other.Actor && Spell == other.Spell && Targets.SequenceEqual(other.Targets);

    public override int GetHashCode() => HashCode.Combine(Actor, Spell, Targets.Count);
}
