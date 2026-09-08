using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Rounds;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Builds the combat timeline from the speed choices: every Quick slot before every Standard slot, initiative
/// descending within a speed, ties broken by player slot then creature id so that the order is deterministic.
/// </summary>
public static class TimelineBuilder
{
    public static CombatTimeline Build(IReadOnlyList<CreatureSnapshot> creatures, IEnumerable<SpeedChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(choices);

        var slots = choices.Select(choice =>
        {
            var creature = creatures.FirstOrDefault(candidate => candidate.Id == choice.Creature)
                ?? throw new InvalidOperationException($"Speed choice for creature {choice.Creature} which is not in the match.");
            return new ActivationSlot(creature.Owner, creature.Id, choice.Speed, creature.CurrentInitiative);
        });

        return CombatTimeline.Of(slots
            .OrderBy(slot => slot.Speed)
            .ThenByDescending(slot => slot.Initiative.Value)
            .ThenBy(slot => slot.Owner)
            .ThenBy(slot => slot.Creature.Value));
    }
}
