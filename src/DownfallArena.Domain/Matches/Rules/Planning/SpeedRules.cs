using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Rules of the Speed sub-phase: every living, non-stunned creature gets exactly one speed choice from its owner.
/// </summary>
public static class SpeedRules
{
    public static Result ValidateChoice(PlayerSlot slot, SpeedChoice choice, IReadOnlyList<CreatureSnapshot> creatures)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(creatures);

        var creature = creatures.FirstOrDefault(candidate => candidate.Id == choice.Creature);
        if (creature is null)
        {
            return Result.Failure(PlanningErrors.UnknownCreature);
        }

        if (creature.Owner != slot)
        {
            return Result.Failure(PlanningErrors.NotYourCreature);
        }

        if (creature.IsDead)
        {
            return Result.Failure(PlanningErrors.CreatureDead);
        }

        return creature.IsStunned ? Result.Failure(PlanningErrors.CreatureStunned) : Result.Success();
    }

    public static SpeedGateResult Evaluate(IReadOnlyList<CreatureSnapshot> creatures, Round round)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(round);

        var player1 = Missing(PlayerSlot.Player1, creatures, round);
        var player2 = Missing(PlayerSlot.Player2, creatures, round);
        return new SpeedGateResult(player1.Count == 0 && player2.Count == 0, player1, player2);
    }

    private static List<CreatureId> Missing(PlayerSlot slot, IReadOnlyList<CreatureSnapshot> creatures, Round round) =>
        creatures
            .Where(creature => creature.Owner == slot && creature.IsAlive && !creature.IsStunned && round.SpeedChoiceOf(creature.Id) is null)
            .Select(creature => creature.Id)
            .ToList();
}
