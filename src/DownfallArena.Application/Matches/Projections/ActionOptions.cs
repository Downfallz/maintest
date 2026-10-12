using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The player's creature whose slot has come up, under a rule set that declares no intent (ADR 0110): every
/// spell it can cast now, each with its legal targets on the board as it stands. The player picks one spell and
/// its targets, and the action resolves on confirmation.
/// </summary>
/// <remarks>
/// Each entry is the <see cref="TargetOptions" /> a revealed intent of that spell would have offered, so a
/// choice of spell narrows this to exactly what a target decision reads. The match never asks for a creature
/// with nothing to cast -- its slot is skipped -- so there is always at least one spell, and every spell listed
/// has a legal target. Spells are in ordinal id order, the order intents offer them in.
/// </remarks>
public sealed record ActionOptions(CreatureId Actor, IReadOnlyList<TargetOptions> Spells)
{
    /// <summary>
    /// What a creature is offered on a board: every spell it knows that it can pay for now and that has a legal
    /// target there, in ordinal id order. The one reading of it, for the match's projection and for every
    /// hypothetical board an agent plays out, so a rollout offers what the match would.
    /// </summary>
    public static ActionOptions Of(CreatureSnapshot actor, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        var spells = actor.KnownSpells
            .Where(spell => ActionRules.IsCastableNow(actor, spell, creatures, resources))
            .OrderBy(spell => spell.Value, StringComparer.Ordinal)
            .Select(spell => new TargetOptions(actor.Id, spell, TargetingRules.LegalTargets(actor, resources.GetSpell(spell), creatures)))
            .ToList();
        return new ActionOptions(actor.Id, spells);
    }

    /// <summary>The spells offered, in order.</summary>
    public IReadOnlyList<SpellId> SpellIds => [.. Spells.Select(option => option.Spell)];

    /// <summary>The targets offered with a spell, or <c>null</c> when the spell is not offered.</summary>
    public TargetOptions? For(SpellId? spell) => Spells.FirstOrDefault(option => option.Spell == spell);

    public bool Equals(ActionOptions? other) =>
        other is not null && Actor == other.Actor && Spells.SequenceEqual(other.Spells);

    public override int GetHashCode() => HashCode.Combine(Actor, Spells.Count);
}
