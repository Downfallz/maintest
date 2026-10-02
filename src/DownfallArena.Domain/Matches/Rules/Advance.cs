using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// Answers what a board of snapshots would be after a step a match has not played: a combat action, the
/// cleanup that ends a round, the start of the next, the purchases of an evolution (ADR 0047, ADR 0094). It
/// restores the creatures from the snapshots and runs the very rules <see cref="Match"/> runs on them --
/// <see cref="CombatExecution"/>, <see cref="UpkeepRules"/>, <see cref="WinCondition"/>,
/// <see cref="Creature.BuyTier"/> -- so there is one applier and not a second one to keep in agreement with
/// it. The snapshots handed in are never changed; the creatures that were restored never leave.
/// <para>
/// It is a hypothetical board and not a match: no round, no timeline, no event. Whether the match would have
/// ended after a cleanup is <see cref="Outcome"/>, asked before the start of the next round the way
/// <see cref="Match"/> asks it. Nor a choice: what an evolution buys is the caller's to pick, and
/// <see cref="Buy"/> only buys it.
/// </para>
/// </summary>
public static class Advance
{
    /// <summary>
    /// The board after one combat action: resolved on the snapshots as the match resolves it, applied to the
    /// restored creatures as the match applies it. The random source decides the critical roll, which is how
    /// a caller asks for the plain outcome or the critical one — and the speed decides whether there is a roll
    /// to ask for at all, since a Quick cast never crits.
    /// </summary>
    public static AdvancedAction Action(
        CombatAction action,
        IReadOnlyList<CreatureSnapshot> board,
        IGameResources resources,
        RuleSet rules,
        IRandomSource random,
        Speed speed)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);

        var resolution = ResolutionRules.Resolve(action, board, resources, rules, random, speed);
        var creatures = Restore(board, resources);
        var applied = CombatExecution.Apply(resolution, creatures);
        return new AdvancedAction(resolution, applied, Snapshots(creatures));
    }

    /// <summary>The board after the cleanup that ends a round: every condition counts one round down.</summary>
    public static IReadOnlyList<CreatureSnapshot> Cleanup(IReadOnlyList<CreatureSnapshot> board, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resources);

        var creatures = Restore(board, resources);
        UpkeepRules.Cleanup(creatures);
        return Snapshots(creatures);
    }

    /// <summary>
    /// How the match would end after the round this board closes, or <c>null</c> when it would go on: the win
    /// condition the match checks after its cleanup, on the two teams the board's owners form.
    /// </summary>
    public static MatchOutcome? Outcome(IReadOnlyList<CreatureSnapshot> board, IGameResources resources, int completedRound, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

        var creatures = Restore(board, resources);
        return WinCondition.Evaluate(TeamOf(PlayerSlot.Player1, creatures), TeamOf(PlayerSlot.Player2, creatures), completedRound, rules);
    }

    /// <summary>
    /// How the match would end on this board if a team is wiped, or <c>null</c> while both stand: the check the
    /// match makes after every action and every upkeep, and ends on at once (ADR 0083). Read off the snapshots
    /// rather than restored creatures: a rollout asks it after every slot, and a team is defeated exactly when
    /// none of its creatures is alive, which a snapshot says as it is.
    /// </summary>
    public static MatchOutcome? Elimination(IReadOnlyList<CreatureSnapshot> board)
    {
        ArgumentNullException.ThrowIfNull(board);

        return WinCondition.Elimination(Defeated(board, PlayerSlot.Player1), Defeated(board, PlayerSlot.Player2));
    }

    private static bool Defeated(IReadOnlyList<CreatureSnapshot> board, PlayerSlot owner) =>
        !board.Any(creature => creature.Owner == owner && creature.IsAlive);

    /// <summary>
    /// The board after the automatic steps that start a round: the energy gain, then the ongoing effects in
    /// the order the match applies them.
    /// </summary>
    public static IReadOnlyList<CreatureSnapshot> StartOfRound(IReadOnlyList<CreatureSnapshot> board, IGameResources resources, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);

        var creatures = Restore(board, resources);
        UpkeepRules.EnergyGain(creatures, rules);
        UpkeepRules.OngoingEffects(creatures);
        return Snapshots(creatures);
    }

    /// <summary>
    /// The board after an evolution's purchases are revealed: every package bought at once, both players'
    /// together, through the purchase the match makes (ADR 0089). The picks are the caller's to make legal, as
    /// they are a player's: a purchase the creature refuses is a bug in the caller, not a rule, and throws
    /// before anything is bought.
    /// </summary>
    public static IReadOnlyList<CreatureSnapshot> Buy(IReadOnlyList<CreatureSnapshot> board, IReadOnlyList<EvolutionChoice> choices, IGameResources resources)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(resources);

        var creatures = Restore(board, resources);
        foreach (var choice in choices)
        {
            var creature = creatures.Find(candidate => candidate.Id == choice.Creature)
                ?? throw new InvalidOperationException($"A purchase for creature {choice.Creature}, which is not on the board.");
            var bought = creature.BuyTier(resources.GetTier(choice.Tier));
            if (bought.IsFailure)
            {
                throw new InvalidOperationException($"Creature {choice.Creature} refused {choice.Tier}: {bought.Error.Message}");
            }
        }

        return Snapshots(creatures);
    }

    private static List<Creature> Restore(IReadOnlyList<CreatureSnapshot> board, IGameResources resources) =>
        [.. board.Select(snapshot => Restore(snapshot, resources))];

    private static Creature Restore(CreatureSnapshot snapshot, IGameResources resources)
    {
        var definition = resources.GetCreature(snapshot.DefinitionId);
        return Creature.Restore(snapshot, definition, [.. snapshot.AcquiredTiers.Select(resources.GetTier)]);
    }

    private static List<CreatureSnapshot> Snapshots(List<Creature> creatures) =>
        [.. creatures.Select(creature => creature.Snapshot())];

    private static Team TeamOf(PlayerSlot owner, List<Creature> creatures) =>
        Team.Form(owner, creatures.FindAll(creature => creature.Owner == owner));
}
