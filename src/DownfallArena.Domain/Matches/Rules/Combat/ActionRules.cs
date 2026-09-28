using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Rules of the Activation sub-phase (ADR 0083): the owner of the revealed intent binds targets that satisfy the
/// spell on the board as it stands, and the action resolves at once. Any targeting failure blocks the action
/// here. A creature that cannot act when its slot comes up -- dead, stunned, unable to pay or to cast, or left
/// with no legal target -- is not asked: it is revealed with no targets and fizzles.
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

        var spell = resources.GetSpell(action.Spell);
        if (!TargetingRules.LegalTargets(actor, spell, creatures).IsCastable)
        {
            return action.Targets.Count == 0 ? Result.Success() : Result.Failure(CombatErrors.NoLegalTarget);
        }

        var report = TargetingRules.Check(actor, spell, action.Targets, creatures);
        return report.FirstFailure is { } failure ? Result.Failure(failure.Error) : Result.Success();
    }

    /// <summary>
    /// Whether the creature can take its slot: alive, not stunned, still able to cast its spell, and with a
    /// legal target for it. When it cannot, its owner is not asked (ADR 0083).
    /// </summary>
    public static bool CanTakeItsSlot(CombatIntent intent, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        var actor = creatures.FirstOrDefault(candidate => candidate.Id == intent.Actor);
        if (actor is null || IntentRules.CanAct(actor, intent.Spell, resources).IsFailure)
        {
            return false;
        }

        return TargetingRules.LegalTargets(actor, resources.GetSpell(intent.Spell), creatures).IsCastable;
    }

    public static ActionGateResult Evaluate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var next = round.NextSlot;
        return new ActionGateResult(next is null, round.Timeline.Count - round.ActivationCursor.Index, next?.Creature);
    }
}
