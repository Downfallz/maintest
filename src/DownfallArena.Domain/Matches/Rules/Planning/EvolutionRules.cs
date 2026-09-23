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
        if (HasEvolved(creature, round))
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
    /// The sub-phase is complete when no player has an effective pick left. Picks are capped by the rule set's
    /// schedule and by the creatures <see cref="Offers"/> names, since each buys at most one package a round
    /// (ADR 0066), so a player down to one living creature has one pick; a player who passed has none. A round
    /// the schedule gives no opportunity is complete as soon as it opens, which is how an even round costs
    /// nobody an input rather than stalling for two passes.
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

        return Math.Min(remaining, Offers(slot, creatures, round, resources).Count);
    }

    /// <summary>
    /// The creatures a pick of this player's can go to this round, each with the packages it can buy: the one
    /// answer the gate counts and the options a player is shown, so the two cannot disagree (ADR 0066).
    /// </summary>
    public static IReadOnlyList<EvolutionOffer> Offers(
        PlayerSlot slot,
        IReadOnlyList<CreatureSnapshot> creatures,
        Round round,
        IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(resources);

        return [.. creatures
            .Where(creature => creature.Owner == slot && creature.IsAlive && !HasEvolved(creature, round))
            .Select(creature => new EvolutionOffer(creature.Id, TierEligibility.AvailableTiers(creature, resources)))
            .Where(offer => offer.Tiers.Count > 0)];
    }

    /// <summary>Whether the creature has already bought a package in this round.</summary>
    private static bool HasEvolved(CreatureSnapshot creature, Round round) =>
        round.EvolutionChoicesOf(creature.Owner).Any(choice => choice.Creature == creature.Id);
}
