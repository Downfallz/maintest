using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// Answers what a board of snapshots would be after a step a match has not played: a combat action, the
/// cleanup that ends a round, the start of the next (ADR 0047). It restores the creatures from the snapshots
/// and runs the very rules <see cref="Match"/> runs on them -- <see cref="CombatExecution"/>,
/// <see cref="UpkeepRules"/>, <see cref="WinCondition"/> -- so there is one applier and not a second one to
/// keep in agreement with it. The snapshots handed in are never changed; the creatures that were restored
/// never leave.
/// <para>
/// It is a hypothetical board and not a match: no round, no timeline, no event. Whether the match would have
/// ended after a cleanup is <see cref="Outcome"/>, asked before the start of the next round the way
/// <see cref="Match"/> asks it. Nor any evolution: a board advanced past a round assumes nobody unlocks a
/// spell in it, which is the one creature-changing step of a round not offered here.
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

    private static List<Creature> Restore(IReadOnlyList<CreatureSnapshot> board, IGameResources resources) =>
        [.. board.Select(snapshot => Restore(snapshot, resources))];

    private static Creature Restore(CreatureSnapshot snapshot, IGameResources resources)
    {
        var definition = resources.GetCreature(snapshot.DefinitionId);
        return Creature.Restore(snapshot, definition, resources.GetTalentTree(definition.TalentTree));
    }

    private static List<CreatureSnapshot> Snapshots(List<Creature> creatures) =>
        [.. creatures.Select(creature => creature.Snapshot())];

    private static Team TeamOf(PlayerSlot owner, List<Creature> creatures) =>
        Team.Form(owner, creatures.FindAll(creature => creature.Owner == owner));
}
