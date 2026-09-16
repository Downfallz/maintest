using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.Domain.Matches.Rules.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// Answers what a board of snapshots would be after a step a match has not played: a combat action, the
/// cleanup that ends a round, the upkeep that starts the next (ADR 0047). It restores the creatures from the
/// snapshots and runs the very rules <see cref="Match"/> runs on them -- <see cref="CombatExecution"/>,
/// <see cref="UpkeepRules"/> -- so there is one applier and not a second one to keep in agreement with it. The
/// snapshots handed in are never changed; the creatures that were restored never leave.
/// <para>
/// It is a hypothetical board and not a match: no round, no timeline, no win check, no event. Whether the
/// match would have ended between a cleanup and the next upkeep is <see cref="WinCondition"/>'s question, and a
/// caller that advances past a round asks it in between. Nor any evolution: a board advanced past a round
/// assumes nobody unlocks a spell in it, which is the one creature-changing step of a round not offered here.
/// </para>
/// </summary>
public static class Advance
{
    /// <summary>
    /// The board after one combat action: resolved on the snapshots as the match resolves it, applied to the
    /// restored creatures as the match applies it. The random source decides the critical roll, which is how
    /// a caller asks for the plain outcome or the critical one.
    /// </summary>
    public static AdvancedAction Action(
        CombatAction action,
        IReadOnlyList<CreatureSnapshot> board,
        IGameResources resources,
        RuleSet rules,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);

        var resolution = ResolutionRules.Resolve(action, board, resources, rules, random);
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
    /// The board after the upkeep that starts a round: the energy gain, then the ongoing effects in the order
    /// the match applies them.
    /// </summary>
    public static IReadOnlyList<CreatureSnapshot> Upkeep(IReadOnlyList<CreatureSnapshot> board, IGameResources resources, RuleSet rules)
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
        [.. board.Select(snapshot => Creature.Restore(snapshot, resources.GetCreature(snapshot.DefinitionId)))];

    private static List<CreatureSnapshot> Snapshots(List<Creature> creatures) =>
        [.. creatures.Select(creature => creature.Snapshot())];
}
