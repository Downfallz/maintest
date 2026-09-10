using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Events;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli;

/// <summary>
/// Narrates a match from its domain events, one readable line per event that matters to a spectator.
/// </summary>
internal sealed class ConsoleMatchLog(TextWriter writer) : IDomainEventListener
{
    public Type EventType => typeof(IDomainEvent);

    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (Describe(domainEvent) is { } line)
        {
            writer.WriteLine(line);
        }

        return Task.CompletedTask;
    }

    private static string? Describe(IDomainEvent domainEvent) =>
        domainEvent switch
        {
            MatchStarted started => $"Match started (content {started.ContentHash[..Math.Min(8, started.ContentHash.Length)]}).",
            RoundStarted round => $"--- Round {round.RoundId.Number} ---",
            EvolutionChoiceSubmitted evolution => $"{evolution.Slot}: creature {evolution.Choice.Creature} unlocks {evolution.Choice.Spell.Value}.",
            EvolutionPassed passed => $"{passed.Slot} passes.",
            TimelineBuilt timeline => "Timeline: " + string.Join(", ", timeline.Timeline.Slots.Select(slot => $"{slot.Creature} ({slot.Speed})")),
            ActionRevealed revealed => $"Creature {revealed.Action.Actor} reveals {revealed.Action.Spell.Value} on [{string.Join(", ", revealed.Action.Targets)}].",
            CombatActionResolved resolved => Describe(resolved),
            OngoingEffectsApplied ongoing => Describe(ongoing),
            MatchEnded ended => ended.Outcome.IsDraw
                ? $"Match ended in a draw after round {ended.RoundId.Number} ({ended.Outcome.Reason})."
                : $"Match ended: {ended.Outcome.Winner} wins after round {ended.RoundId.Number} ({ended.Outcome.Reason}).",
            _ => null,
        };

    /// <summary>
    /// The start of the round, in the order it was applied: the energy energyRegenerations gave, the healing, then the
    /// bleeds (ADR 0019, ADR 0020). Nothing when none of the three ticked.
    /// </summary>
    private static string? Describe(OngoingEffectsApplied ongoing)
    {
        var parts = new List<string>();
        if (ongoing.EnergyRegenerationTicks.Count > 0)
        {
            parts.Add("Energy regenerations: " + string.Join(", ", ongoing.EnergyRegenerationTicks.Select(tick => $"creature {tick.Creature} gains {tick.Gained} energy")));
        }

        if (ongoing.RegenerationTicks.Count > 0)
        {
            parts.Add("Regenerations: " + string.Join(", ", ongoing.RegenerationTicks.Select(tick => $"creature {tick.Creature} heals {tick.Healed}")));
        }

        if (ongoing.BleedTicks.Count > 0)
        {
            parts.Add("Bleeds: " + string.Join(", ", ongoing.BleedTicks.Select(tick => $"creature {tick.Creature} takes {tick.Damage}")));
        }

        return parts.Count == 0 ? null : string.Join(". ", parts);
    }

    private static string Describe(CombatActionResolved resolved)
    {
        var resolution = resolved.Resolution;
        if (resolution.Fizzled)
        {
            return $"  Creature {resolution.Action.Actor}: {resolution.Action.Spell.Value} fizzles ({resolution.FizzleReason?.Code}).";
        }

        var crit = resolution.IsCritical ? " CRITICAL" : string.Empty;
        // What the board took, not what the action aimed for: a hit of seven on a creature with two health
        // left would otherwise be logged as seven, next to the creature dying with five unaccounted for.
        var outcomes = resolved.AppliedOutcomes.Count == 0
            ? "nothing"
            : string.Join(", ", resolved.AppliedOutcomes.Select(outcome => outcome.ToString()));
        return $"  Creature {resolution.Action.Actor}: {resolution.Action.Spell.Value}{crit} -> {outcomes}";
    }
}
