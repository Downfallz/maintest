using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Scenarios;

/// <summary>
/// The rounds a recorded match can be cut at (docs/tabletop/scenarios-plan.md, stage S1), read by rebuilding it
/// from its seed one journal entry at a time with <see cref="MatchReplay" />. Nothing is written and nobody is
/// asked anything.
/// </summary>
public sealed class ScenarioRounds(MatchReplay replay, MatchStepper stepper, IMatchRepository matches, IEnumerable<IMatchMemory> memories)
{
    /// <summary>
    /// Every round the match's board reached with no answer held, and did not end in, as far as its journal
    /// rebuilds. A round whose top rebuilds is offered even when a later line of it does not: that is the round
    /// an engine change shows in, the one to reproduce.
    /// </summary>
    /// <remarks>
    /// The match is rebuilt under an id of its own and forgotten once read: the source's id may be a table a host
    /// holds in memory, which a rebuild under it would take back to wherever the reading stopped.
    /// </remarks>
    public async Task<ScenarioReading> ReadAsync(JournaledMatch source, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entries);

        var aside = source with { MatchId = MatchId.New() };
        try
        {
            return await AsideAsync(aside, entries, cancellationToken);
        }
        finally
        {
            await new ScenarioBoards.Aside(matches, memories).ForgetAsync(aside.MatchId);
        }
    }

    private async Task<ScenarioReading> AsideAsync(JournaledMatch source, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken)
    {
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
