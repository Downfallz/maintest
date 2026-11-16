using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Creatures;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>What each seat played in a finished match, read off its final board and the events it raised.</summary>
public static class PlayedTeams
{
    /// <summary>
    /// Every creature of both seats, the first seat's first: its definition, the mutations it owns, whether it is
    /// alive on the final board, and the resolved casts the events credit to it.
    /// </summary>
    /// <param name="final">The final board, as the first seat reads it: its allies are the first seat's creatures.</param>
    /// <param name="events">Every event the match raised, in order.</param>
    /// <param name="seatName">What a result calls a seat.</param>
    public static IReadOnlyList<PlayedCreature> Of(PlayerBoardState final, IEnumerable<IMatchEvent> events, Func<PlayerSlot, string> seatName)
    {
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(seatName);
        var casts = new Dictionary<CreatureId, Dictionary<string, int>>();
        foreach (var resolved in events.OfType<CombatActionResolved>().Where(resolved => !resolved.Resolution.Fizzled))
        {
            var action = resolved.Resolution.Action;
            if (!casts.TryGetValue(action.Actor, out var mine))
            {
                mine = new Dictionary<string, int>(StringComparer.Ordinal);
                casts[action.Actor] = mine;
            }

            mine[action.Spell.Value] = mine.GetValueOrDefault(action.Spell.Value) + 1;
        }

        return
        [
            .. final.Allies.Select(creature => Played(creature, PlayerSlot.Player1)),
            .. final.Enemies.Select(creature => Played(creature, PlayerSlot.Player2)),
        ];

        PlayedCreature Played(CreatureSnapshot creature, PlayerSlot slot) => new(
            seatName(slot),
            creature.DefinitionId.Value,
            [.. creature.AcquiredTiers.Select(tier => tier.Value).Order(StringComparer.Ordinal)],
            creature.IsAlive,
            casts.TryGetValue(creature.Id, out var cast) ? cast : new Dictionary<string, int>(StringComparer.Ordinal));
    }
}
