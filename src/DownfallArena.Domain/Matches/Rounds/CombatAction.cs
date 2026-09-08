using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A revealed intent bound to its targets.
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

    public bool Equals(CombatAction? other) =>
        other is not null && Actor == other.Actor && Spell == other.Spell && Targets.SequenceEqual(other.Targets);

    public override int GetHashCode() => HashCode.Combine(Actor, Spell, Targets.Count);
}
