using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Randomness;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Builds the combat timeline from the speed choices: every Quick slot before every Standard slot, initiative
/// descending within a speed, and a tie rolled off on a d20 (ADR 0063).
/// </summary>
public static class TimelineBuilder
{
    /// <summary>The die a tie is rolled on.</summary>
    public const int TieDie = 20;

    public static CombatTimeline Build(IReadOnlyList<CreatureSnapshot> creatures, IEnumerable<SpeedChoice> choices, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(random);

        var slots = choices.Select(choice =>
        {
            var creature = creatures.FirstOrDefault(candidate => candidate.Id == choice.Creature)
                ?? throw new InvalidOperationException($"Speed choice for creature {choice.Creature} which is not in the match.");
            return new ActivationSlot(creature.Owner, creature.Id, choice.Speed, creature.CurrentInitiative);
        });

        // Player slot then creature id is no longer an order, only the order the tied creatures roll in, so a
        // seeded match replays its rolls exactly.
        var tied = slots
            .OrderBy(slot => slot.Speed)
            .ThenByDescending(slot => slot.Initiative.Value)
            .ThenBy(slot => slot.Owner)
            .ThenBy(slot => slot.Creature.Value);

        var ordered = new List<ActivationSlot>();
        var rolls = new Dictionary<CreatureId, List<int>>();
        foreach (var tie in TieOrderRules.Ties(tied))
        {
            ordered.AddRange(RollOff(tie, random, rolls));
        }

        // In timeline order, so a reader lists the dice the way the table rolled them.
        IReadOnlyList<RollOff> rollOffs =
            [.. ordered.Where(slot => rolls.ContainsKey(slot.Creature)).Select(slot => new RollOff(slot.Creature, rolls[slot.Creature]))];
        return CombatTimeline.Of(ordered).WithRollOffs(rollOffs);
    }

    /// <summary>
    /// Every tied creature rolls the die and the highest acts first; creatures that roll the same number roll
    /// again among themselves. What a table does with a d20, so the seat decides nothing. A tie held by one
    /// side alone rolls nothing: its owner orders it (<see cref="TieOrderRules"/>), and so the owner orders
    /// their own creatures in a mixed tie too, among the places the rolls gave their side.
    /// </summary>
    private static List<ActivationSlot> RollOff(List<ActivationSlot> tied, IRandomSource random, Dictionary<CreatureId, List<int>> rolls)
    {
        if (tied.Select(slot => slot.Owner).Distinct().Count() == 1)
        {
            return tied;
        }

        var rolled = tied.Select(slot => (Slot: slot, Roll: random.NextInt32(1, TieDie + 1))).ToList();
        foreach (var (slot, roll) in rolled)
        {
            if (!rolls.TryGetValue(slot.Creature, out var made))
            {
                made = [];
                rolls[slot.Creature] = made;
            }

            made.Add(roll);
        }

        var ordered = new List<ActivationSlot>();
        foreach (var same in rolled.GroupBy(one => one.Roll).OrderByDescending(group => group.Key))
        {
            ordered.AddRange(RollOff([.. same.Select(one => one.Slot)], random, rolls));
        }

        return ordered;
    }
}
