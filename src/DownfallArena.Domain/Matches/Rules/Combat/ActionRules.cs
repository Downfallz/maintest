using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Rules of the RevealAndTarget sub-phase: the owner of the revealed intent binds targets that satisfy the spell.
/// Any targeting failure blocks the action here; at resolution time, only global failures fizzle it.
/// </summary>
public static class ActionRules
{
    public static Result ValidateAction(PlayerSlot slot, CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        var actor = creatures.FirstOrDefault(candidate => candidate.Id == action.Actor);
        if (actor is null)
        {
            return Result.Failure(CombatErrors.UnknownCreature);
        }

        if (actor.Owner != slot)
        {
            return Result.Failure(CombatErrors.NotYourCreature);
        }

        var canAct = IntentRules.CanAct(actor, action.Spell, resources);
        if (canAct.IsFailure)
        {
            return canAct;
        }

        var report = TargetingRules.Check(actor, resources.GetSpell(action.Spell), action.Targets, creatures);
        return report.FirstFailure is { } failure ? Result.Failure(failure.Error) : Result.Success();
    }

    public static ActionGateResult Evaluate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var next = round.NextSlotToReveal;
        return new ActionGateResult(next is null, round.Timeline.Count - round.RevealCursor.Index, next?.Creature);
    }
}
