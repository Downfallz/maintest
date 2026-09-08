using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Rules of the Evolution sub-phase: who may unlock what, and when the sub-phase is complete.
/// </summary>
public static class EvolutionRules
{
    /// <summary>
    /// Validates a choice without applying it. The aggregate unlocks the spell only after the round accepted the choice.
    /// </summary>
    public static Result ValidateChoice(
        PlayerSlot slot,
        EvolutionChoice choice,
        IReadOnlyList<CreatureSnapshot> creatures,
        Round round,
        IGameResources resources,
        RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

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

        if (round.HasPassedEvolution(slot) || round.EvolutionChoicesOf(slot).Count >= rules.EvolutionPicksPerRound)
        {
            return Result.Failure(PlanningErrors.NoPicksLeft);
        }

        if (creature.KnowsSpell(choice.Spell))
        {
            return Result.Failure(PlanningErrors.SpellAlreadyKnown);
        }

        var tree = resources.GetTalentTree(creature.TalentTree);
        return TalentUnlocks.UnlockableSpells(creature, tree).Contains(choice.Spell)
            ? Result.Success()
            : Result.Failure(PlanningErrors.SpellNotUnlockable);
    }

    /// <summary>
    /// The sub-phase is complete when no player has an effective pick left: picks are capped by the rule set and
    /// by how many spells the player's living creatures can actually unlock, and a player who passed has none.
    /// </summary>
    public static EvolutionGateResult Evaluate(
        IReadOnlyList<CreatureSnapshot> creatures,
        Round round,
        IGameResources resources,
        RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

        var player1 = RemainingPicks(PlayerSlot.Player1, creatures, round, resources, rules);
        var player2 = RemainingPicks(PlayerSlot.Player2, creatures, round, resources, rules);
        return new EvolutionGateResult(player1 == 0 && player2 == 0, player1, player2);
    }

    private static int RemainingPicks(
        PlayerSlot slot,
        IReadOnlyList<CreatureSnapshot> creatures,
        Round round,
        IGameResources resources,
        RuleSet rules)
    {
        var remaining = round.HasPassedEvolution(slot) ? 0 : Math.Max(0, rules.EvolutionPicksPerRound - round.EvolutionChoicesOf(slot).Count);
        if (remaining == 0)
        {
            return 0;
        }

        var unlockable = creatures
            .Where(creature => creature.Owner == slot && creature.IsAlive)
            .Sum(creature => TalentUnlocks.UnlockableSpells(creature, resources.GetTalentTree(creature.TalentTree)).Count);

        return Math.Min(remaining, unlockable);
    }
}
