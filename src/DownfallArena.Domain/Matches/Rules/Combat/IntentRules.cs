using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Rules of the IntentSelection sub-phase: a player declares, for each own creature on the timeline, a spell the
/// creature knows. Whether it can pay is asked when its slot comes up, not when the card goes down (ADR 0107):
/// energy can arrive in between, from an ally acting earlier, and a creature still short then fizzles (ADR 0083).
/// </summary>
public static class IntentRules
{
    public static Result ValidateIntent(PlayerSlot slot, CombatIntent intent, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        var actor = creatures.FirstOrDefault(candidate => candidate.Id == intent.Actor);
        if (actor is null)
        {
            return Result.Failure(CombatErrors.UnknownCreature);
        }

        if (actor.Owner != slot)
        {
            return Result.Failure(CombatErrors.NotYourCreature);
        }

        return CanDeclare(actor, intent.Spell);
    }

    public static IntentGateResult Evaluate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var missing = round.Timeline.Slots
            .Select(slot => slot.Creature)
            .Where(creature => !round.HasIntent(creature))
            .ToList();

        return new IntentGateResult(missing.Count == 0, missing);
    }

    /// <summary>
    /// The checks an action and its resolution make: everything a declaration checks, and that the creature can
    /// pay for the spell now.
    /// </summary>
    internal static Result CanAct(CreatureSnapshot actor, SpellId spellId, IGameResources resources)
    {
        var declarable = CanDeclare(actor, spellId);
        if (declarable.IsFailure)
        {
            return declarable;
        }

        var spell = resources.GetSpell(spellId);
        return actor.Energy < spell.Stats.Cost ? Result.Failure(CombatErrors.NotEnoughEnergy) : Result.Success();
    }

    /// <summary>The checks a declaration makes: alive, not stunned, knows the spell. Not the price (ADR 0107).</summary>
    private static Result CanDeclare(CreatureSnapshot actor, SpellId spellId)
    {
        if (actor.IsDead)
        {
            return Result.Failure(CombatErrors.ActorDead);
        }

        if (actor.IsStunned)
        {
            return Result.Failure(CombatErrors.ActorStunned);
        }

        return actor.KnowsSpell(spellId) ? Result.Success() : Result.Failure(CombatErrors.SpellNotKnown);
    }
}
