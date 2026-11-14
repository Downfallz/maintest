using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The rounds a recorded match can be cut at (docs/tabletop/scenarios-plan.md, stage S1), read by rebuilding it
/// from its seed one journal entry at a time with <see cref="MatchReplay" />. Nothing is written and nobody is
/// asked anything.
/// </summary>
public sealed class ScenarioRounds(MatchReplay replay, MatchStepper stepper)
{
    /// <summary>
    /// Every round the match's board reached with no answer held, and did not end in, as far as its journal
    /// rebuilds. A round whose top rebuilds is offered even when a later line of it does not: that is the round
    /// an engine change shows in, the one to reproduce.
    /// </summary>
    public async Task<ScenarioReading> ReadAsync(JournaledMatch source, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entries);

        var rounds = new List<RoundCut>();
        var built = await replay.RebuildAsync(source, [], cancellationToken);
        if (built.IsFailure)
        {
            return new ScenarioReading(rounds, built.Error);
        }

        var held = built.Value;
        for (var line = 0; ; line++)
        {
            var seat = await stepper.SeatAsync(source.MatchId, PlayerSlot.Player1, held, cancellationToken);
            if (seat.IsFailure)
            {
                return new ScenarioReading(rounds, seat.Error);
            }

            var board = seat.Value.Board;
            if (board.State == MatchState.Ended)
            {
                // The top of the round a match ended in leads nowhere but to that end once it is played again as it
                // was; it is still a round played, and it is offered like the others.
                return new ScenarioReading(rounds, null);
            }

            if (board.RoundNumber is { } round && held.IsEmpty && (rounds.Count == 0 || rounds[^1].Round < round))
            {
                rounds.Add(new RoundCut(round, line));
            }

            if (line == entries.Count)
            {
                return new ScenarioReading(rounds, null);
            }

            var taken = await replay.ContinueAsync(source, held, [entries[line]], line, cancellationToken);
            if (taken.IsFailure)
            {
                return new ScenarioReading(rounds, taken.Error);
            }

            held = taken.Value;
        }
    }
}
