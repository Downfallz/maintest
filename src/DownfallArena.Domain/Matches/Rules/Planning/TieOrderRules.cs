using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Rules of the TieOrder sub-phase (ADR 0063). The roll-off decides which places on the timeline each side
/// won; a player then orders their own creatures among the places their side won in the same tie. A player
/// whose creatures tie with none of their own has nothing to order.
/// </summary>
public static class TieOrderRules
{
    /// <summary>
    /// Every tie in which the player holds two places or more: their creatures in each one, in the order the
    /// timeline holds them now. Tied slots sit next to each other, since the timeline is sorted on speed and
    /// initiative before a tie is rolled.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<CreatureId>> TiesOf(CombatTimeline timeline, PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        return
        [
            .. Ties(timeline.Slots)
                .Select(tie => (IReadOnlyList<CreatureId>)[.. tie.Where(entry => entry.Owner == slot).Select(entry => entry.Creature)])
                .Where(mine => mine.Count > 1),
        ];
    }

    public static bool HasTieOrderToGive(CombatTimeline timeline, PlayerSlot slot) => TiesOf(timeline, slot).Count > 0;

    /// <summary>An order names every creature of the player's ties once, and nothing else.</summary>
    public static Result ValidateOrder(PlayerSlot slot, IReadOnlyList<CreatureId> order, CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(timeline);

        var owed = TiesOf(timeline, slot).SelectMany(tie => tie).ToHashSet();
        if (owed.Count == 0)
        {
            return Result.Failure(PlanningErrors.NoTieToOrder);
        }

        return order.Count == owed.Count && order.ToHashSet().SetEquals(owed)
            ? Result.Success()
            : Result.Failure(PlanningErrors.TieOrderMismatch);
    }

    /// <summary>
    /// The progression gate: the sub-phase completes when every player with a tie order to give has given it.
    /// </summary>
    public static TieOrderGateResult Evaluate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        IReadOnlyList<PlayerSlot> waiting =
            [.. new[] { PlayerSlot.Player1, PlayerSlot.Player2 }.Where(slot => HasTieOrderToGive(round.Timeline, slot) && round.TieOrderOf(slot) is null)];
        return new TieOrderGateResult(waiting.Count == 0, waiting);
    }

    /// <summary>
    /// The timeline with each player's tied creatures moved into the places their side won, in the order the
    /// player gave. The places themselves do not move: which side acts where is the roll-off's.
    /// </summary>
    public static CombatTimeline Apply(CombatTimeline timeline, IReadOnlyDictionary<PlayerSlot, IReadOnlyList<CreatureId>> orders)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(orders);

        var ordered = new List<ActivationSlot>(timeline.Count);
        foreach (var tie in Ties(timeline.Slots))
        {
            var queues = tie
                .GroupBy(entry => entry.Owner)
                .ToDictionary(side => side.Key, side => new Queue<ActivationSlot>(InOrder(side, orders.GetValueOrDefault(side.Key))));
            ordered.AddRange(tie.Select(entry => queues[entry.Owner].Dequeue()));
        }

        return CombatTimeline.Of(ordered).WithRollOffs(timeline.RollOffs);
    }

    // A side holding one place in a tie has nothing to order there, and its owner's order, given for another
    // tie, does not name that creature.
    private static IEnumerable<ActivationSlot> InOrder(IGrouping<PlayerSlot, ActivationSlot> side, IReadOnlyList<CreatureId>? order) =>
        order is null || side.Count() < 2 ? side : side.OrderBy(entry => IndexIn(order, entry.Creature));

    private static int IndexIn(IReadOnlyList<CreatureId> order, CreatureId creature)
    {
        for (var index = 0; index < order.Count; index++)
        {
            if (order[index] == creature)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Creature {creature} is tied with its own side but missing from its owner's order.");
    }

    /// <summary>
    /// The ties of a timeline sorted on speed and initiative, as runs of slots that tie with the one before them
    /// (<see cref="ActivationSlot.TiesWith"/>). The one definition of a tie: the builder rolls them and this
    /// class orders them, so the two cannot disagree on what one is.
    /// </summary>
    internal static List<List<ActivationSlot>> Ties(IEnumerable<ActivationSlot> sorted)
    {
        var ties = new List<List<ActivationSlot>>();
        foreach (var entry in sorted)
        {
            if (ties.Count > 0 && ties[^1][0].TiesWith(entry))
            {
                ties[^1].Add(entry);
            }
            else
            {
                ties.Add([entry]);
            }
        }

        return ties;
    }
}
