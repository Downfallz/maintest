using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Rules of the IntentSelection sub-phase: a player declares, for each own creature on the timeline, a known spell
/// the creature can afford.
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

        return CanAct(actor, intent.Spell, resources);
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
    /// The checks shared by intents, actions, and resolution: alive, not stunned, knows the spell, can afford it.
    /// </summary>
    internal static Result CanAct(CreatureSnapshot actor, SpellId spellId, IGameResources resources)
    {
        if (actor.IsDead)
        {
            return Result.Failure(CombatErrors.ActorDead);
        }

        if (actor.IsStunned)
        {
            return Result.Failure(CombatErrors.ActorStunned);
        }

        if (!actor.KnowsSpell(spellId))
        {
            return Result.Failure(CombatErrors.SpellNotKnown);
        }

        var spell = resources.GetSpell(spellId);
        return actor.Energy < spell.Stats.Cost ? Result.Failure(CombatErrors.NotEnoughEnergy) : Result.Success();
    }
}
