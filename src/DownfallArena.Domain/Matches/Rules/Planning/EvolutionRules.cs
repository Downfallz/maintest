using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Rules of the Evolution sub-phase: who may buy which package, when, and when the sub-phase is complete.
/// </summary>
public static class EvolutionRules
{
    /// <summary>
    /// Validates a choice without applying it. The aggregate buys the package only after the round accepted
    /// the choice.
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

        // One question, asked of the rule set: a round that offers no opportunity leaves every player with no
        // pick, which is the same refusal as having spent them (ADR 0056).
        if (PicksLeft(slot, round, rules) == 0)
        {
            return Result.Failure(PlanningErrors.NoPicksLeft);
        }

        // One package per creature per opportunity (ADR 0066): the picks go to different creatures, so no
        // creature climbs two levels in one round and neither pick depends on the other.
        if (HasEvolved(slot, creature, round))
        {
            return Result.Failure(PlanningErrors.CreatureAlreadyEvolved);
        }

        if (!resources.TryGetTier(choice.Tier, out _))
        {
            return Result.Failure(PlanningErrors.UnknownTier);
        }

        if (creature.OwnsTier(choice.Tier))
        {
            return Result.Failure(PlanningErrors.TierAlreadyOwned);
        }

        return TierEligibility.AvailableTiers(creature, resources).Contains(choice.Tier)
            ? Result.Success()
            : Result.Failure(PlanningErrors.TierNotAvailable);
    }

    /// <summary>
    /// The sub-phase is complete when no player has an effective pick left: picks are capped by the rule set's
    /// schedule and by how many of the player's living creatures can still buy something, since each buys at
    /// most one package a round (ADR 0066), and a player who passed has none. A player down to one living
    /// creature therefore has one pick. A round the schedule gives no opportunity is therefore complete as soon as it opens,
    /// which is how an even round costs nobody an input rather than stalling for two passes.
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

    /// <summary>What the schedule and the round's history leave, before asking what there is to buy.</summary>
    private static int PicksLeft(PlayerSlot slot, Round round, RuleSet rules) =>
        round.HasPassedEvolution(slot)
            ? 0
            : Math.Max(0, rules.EvolutionPicksIn(round.Number) - round.EvolutionChoicesOf(slot).Count);

    private static int RemainingPicks(
        PlayerSlot slot,
        IReadOnlyList<CreatureSnapshot> creatures,
        Round round,
        IGameResources resources,
        RuleSet rules)
    {
        var remaining = PicksLeft(slot, round, rules);
        if (remaining == 0)
        {
            return 0;
        }

        // A pick is only effective on a creature that has not bought this round and has something to buy.
        var available = creatures
            .Count(creature => creature.Owner == slot
                && creature.IsAlive
                && !HasEvolved(slot, creature, round)
                && TierEligibility.AvailableTiers(creature, resources).Count > 0);

        return Math.Min(remaining, available);
    }

    /// <summary>Whether the creature has already bought a package in this round.</summary>
    public static bool HasEvolved(PlayerSlot slot, CreatureSnapshot creature, Round round)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(round);
        return round.EvolutionChoicesOf(slot).Any(choice => choice.Creature == creature.Id);
    }
}
