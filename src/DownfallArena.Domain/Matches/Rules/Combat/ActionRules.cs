using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// Rules of the Activation sub-phase (ADR 0083): the owner of the revealed intent binds targets that satisfy the
/// spell on the board as it stands, and the action resolves at once. Any targeting failure blocks the action
/// here. A creature that cannot act when its slot comes up -- dead, stunned, unable to pay or to cast, or left
/// with no legal target -- is not asked: it is revealed with no targets and fizzles. When the rule set declares
/// no Intent, the owner chooses the spell with its targets, and a creature with nothing to cast is skipped
/// (ADR 0110).
/// </summary>
public static class ActionRules
{
    public static Result ValidateAction(PlayerSlot slot, CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        var checkedActor = ActorAndSpell(slot, action, creatures, resources);
        if (checkedActor.IsFailure)
        {
            return checkedActor;
        }

        var (actor, spell) = checkedActor.Value;
        if (!TargetingRules.LegalTargets(actor, spell, creatures).IsCastable)
        {
            return action.Targets.Count == 0 ? Result.Success() : Result.Failure(CombatErrors.NoLegalTarget);
        }

        return Targets(actor, spell, action, creatures);
    }

    /// <summary>
    /// An action whose spell is chosen at the slot (<see cref="CombatDeclaration.AtActivation" />, ADR 0110): the
    /// actor's owner's, a spell it knows and can pay for now, and targets that satisfy it on the board as it
    /// stands. Unlike a revealed intent, a spell with no legal target is refused rather than cast on nothing: the
    /// owner can choose another.
    /// </summary>
    public static Result ValidateChosenAction(PlayerSlot slot, CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        var checkedActor = ActorAndSpell(slot, action, creatures, resources);
        if (checkedActor.IsFailure)
        {
            return checkedActor;
        }

        var (actor, spell) = checkedActor.Value;
        return TargetingRules.LegalTargets(actor, spell, creatures).IsCastable
            ? Targets(actor, spell, action, creatures)
            : Result.Failure(CombatErrors.NoLegalTarget);
    }

    /// <summary>
    /// Whether a creature that declared no intent can take its slot (ADR 0110): alive, not stunned, and knowing a
    /// spell it can pay for now with a legal target. The failure says why not, and its owner is not asked.
    /// </summary>
    public static Result CanChooseAnAction(CreatureSnapshot actor, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        if (actor.IsDead)
        {
            return Result.Failure(CombatErrors.ActorDead);
        }

        if (actor.IsStunned)
        {
            return Result.Failure(CombatErrors.ActorStunned);
        }

        return actor.KnownSpells.Any(spell => IsCastableNow(actor, spell, creatures, resources))
            ? Result.Success()
            : Result.Failure(CombatErrors.NothingToCast);
    }

    /// <summary>
    /// Whether the actor can pay for the spell now and has a legal target for it on the board as it stands: what
    /// a spell offered at the slot must be when no intent was declared (ADR 0110).
    /// </summary>
    public static bool IsCastableNow(CreatureSnapshot actor, SpellId spell, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        return IntentRules.CanAct(actor, spell, resources).IsSuccess
            && TargetingRules.LegalTargets(actor, resources.GetSpell(spell), creatures).IsCastable;
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

    /// <summary>
    /// What an action checks under either declaration before its targets: the actor is in the match and its
    /// owner's, alive, unstunned, knows the spell and can pay for it now.
    /// </summary>
    private static Result<(CreatureSnapshot Actor, Spell Spell)> ActorAndSpell(PlayerSlot slot, CombatAction action, IReadOnlyList<CreatureSnapshot> creatures, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(resources);

        var actor = creatures.FirstOrDefault(candidate => candidate.Id == action.Actor);
        if (actor is null)
        {
            return Result.Failure<(CreatureSnapshot, Spell)>(CombatErrors.UnknownCreature);
        }

        if (actor.Owner != slot)
        {
            return Result.Failure<(CreatureSnapshot, Spell)>(CombatErrors.NotYourCreature);
        }

        var canAct = IntentRules.CanAct(actor, action.Spell, resources);
        return canAct.IsFailure
            ? Result.Failure<(CreatureSnapshot, Spell)>(canAct.Error)
            : Result.Success((actor, resources.GetSpell(action.Spell)));
    }

    private static Result Targets(CreatureSnapshot actor, Spell spell, CombatAction action, IReadOnlyList<CreatureSnapshot> creatures)
    {
        var report = TargetingRules.Check(actor, spell, action.Targets, creatures);
        return report.FirstFailure is { } failure ? Result.Failure(failure.Error) : Result.Success();
    }

    public static ActionGateResult Evaluate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        var next = round.NextSlot;
        return new ActionGateResult(next is null, round.Timeline.Count - round.ActivationCursor.Index, next?.Creature);
    }
}
