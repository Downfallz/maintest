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
    public static IReadOnlyList<IReadOnlyList<CreatureId>> GroupsOf(CombatTimeline timeline, PlayerSlot slot)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        return
        [
            .. Ties(timeline)
                .Select(tie => (IReadOnlyList<CreatureId>)[.. tie.Where(entry => entry.Owner == slot).Select(entry => entry.Creature)])
                .Where(mine => mine.Count > 1),
        ];
    }

    public static bool Owes(CombatTimeline timeline, PlayerSlot slot) => GroupsOf(timeline, slot).Count > 0;

    /// <summary>An order names every creature of the player's ties once, and nothing else.</summary>
    public static Result ValidateOrder(PlayerSlot slot, IReadOnlyList<CreatureId> order, CombatTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(timeline);

        var owed = GroupsOf(timeline, slot).SelectMany(group => group).ToHashSet();
        if (owed.Count == 0)
        {
            return Result.Failure(PlanningErrors.NoTieToOrder);
        }

        return order.Count == owed.Count && order.ToHashSet().SetEquals(owed)
            ? Result.Success()
            : Result.Failure(PlanningErrors.TieOrderMismatch);
    }

    /// <summary>The sub-phase completes when every player who owes an order has submitted one.</summary>
    public static bool CanAdvance(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        return Waiting(round).Count == 0;
    }

    /// <summary>The players who owe an order and have not submitted it.</summary>
    public static IReadOnlyList<PlayerSlot> Waiting(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);

        return [.. new[] { PlayerSlot.Player1, PlayerSlot.Player2 }.Where(slot => Owes(round.Timeline, slot) && round.TieOrderOf(slot) is null)];
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
        foreach (var tie in Ties(timeline))
        {
            var queues = tie
                .GroupBy(entry => entry.Owner)
                .ToDictionary(side => side.Key, side => new Queue<ActivationSlot>(InOrder(side, orders.GetValueOrDefault(side.Key))));
            ordered.AddRange(tie.Select(entry => queues[entry.Owner].Dequeue()));
        }

        return CombatTimeline.Of(ordered);
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

    private static List<List<ActivationSlot>> Ties(CombatTimeline timeline)
    {
        var ties = new List<List<ActivationSlot>>();
        foreach (var entry in timeline.Slots)
        {
            if (ties.Count > 0 && ties[^1][0].Speed == entry.Speed && ties[^1][0].Initiative == entry.Initiative)
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
