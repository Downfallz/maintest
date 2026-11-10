using System.Collections.Concurrent;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Steps a match whose journal is the source of truth (ADR 0159). Before each step the journal is read; the
/// match in memory is used when it stands where the journal ends, and rebuilt from the journal otherwise. Every
/// decision a step takes is written after the others, on the condition that nobody else wrote first.
/// </summary>
/// <remarks>
/// <para>
/// The match in memory is a cache. Losing it -- a process that restarted, another one that wrote the last line
/// -- costs a replay and nothing else, so a caller needs nothing but the <see cref="JournaledMatch" /> between
/// two requests.
/// </para>
/// <para>
/// A decision is applied in memory and then written. When the writing is refused, the match in memory is ahead
/// of its journal and is let go of: the caller is told the match moved (<see cref="JournalErrors.Moved" />) and
/// the next step rebuilds. One step at a time a match: two requests for one table take turns here, since they
/// would otherwise share an aggregate.
/// </para>
/// </remarks>
public sealed class JournaledStepper(MatchStepper stepper, MatchReplay replay, IDecisionJournal journal, IMatchRepository matches)
{
    private readonly ConcurrentDictionary<MatchId, Kept> _kept = new();

    /// <summary>Takes every decision the Player agents owe, until a person is asked or the match is over.</summary>
    public Task<Result<MatchStep>> AdvanceAsync(JournaledMatch match, MatchSeats seats, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(seats);
        return StepAsync(match, held => stepper.AdvanceAsync(match.MatchId, seats, held, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Takes the decision of a seat nobody here answers for, then advances. Refused as the stepper refuses it,
    /// and with <see cref="JournalErrors.Moved" /> when another writer took the match further meanwhile.
    /// </summary>
    public Task<Result<MatchStep>> SubmitAsync(JournaledMatch match, MatchSeats seats, SeatDecision submitted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(seats);
        ArgumentNullException.ThrowIfNull(submitted);
        return StepAsync(match, held => stepper.SubmitAsync(match.MatchId, seats, held, submitted, cancellationToken), cancellationToken);
    }

    /// <summary>What a seat reads: its board and options with the answers it is holding, and its question.</summary>
    public async Task<Result<SeatView>> SeatAsync(JournaledMatch match, PlayerSlot slot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        var kept = _kept.GetOrAdd(match.MatchId, _ => new Kept());
        return await kept.InTurnAsync(async () =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            return loaded.IsFailure
                ? Result.Failure<SeatView>(loaded.Error)
                : await stepper.SeatAsync(match.MatchId, slot, kept.Held, cancellationToken);
        });
    }

    /// <summary>
    /// Lets go of a match this process is done with. The journal is untouched, so the match is rebuilt if
    /// anybody asks for it again; a host that plays many for days would otherwise keep every one.
    /// </summary>
    public async Task ForgetAsync(MatchId matchId, CancellationToken cancellationToken = default)
    {
        if (!_kept.TryGetValue(matchId, out var kept))
        {
            await matches.ForgetAsync(matchId, cancellationToken);
            return;
        }

        await kept.InTurnAsync(async () =>
        {
            await LetGoAsync(matchId, kept, cancellationToken);
            _kept.TryRemove(matchId, out _);
            return true;
        });
    }

    private Task<Result<MatchStep>> StepAsync(JournaledMatch match, Func<HeldAnswers, Task<Result<MatchStep>>> take, CancellationToken cancellationToken)
    {
        var kept = _kept.GetOrAdd(match.MatchId, _ => new Kept());
        return kept.InTurnAsync(async () =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result.Failure<MatchStep>(loaded.Error);
            }

            try
            {
                var step = await take(kept.Held);
                if (step.IsFailure)
                {
                    // A decision refused before it was taken changed nothing. Any other refusal may have come
                    // after part of a step, which the journal knows nothing of.
                    if (!RefusedUntaken(step.Error))
                    {
                        await LetGoAsync(match.MatchId, kept, cancellationToken);
                    }

                    return step;
                }

                if (step.Value.Decided.Count > 0)
                {
                    var written = await journal.AppendAsync(match.Run, loaded.Value, step.Value.Decided, cancellationToken);
                    if (written is null)
                    {
                        await LetGoAsync(match.MatchId, kept, cancellationToken);
                        return Result.Failure<MatchStep>(JournalErrors.Moved);
                    }

                    kept.Position = written;
                }

                kept.Held = step.Value.Held;
                return step;
            }
            catch
            {
                // Whatever stopped the step, the match in memory may no longer be what the journal says.
                await LetGoAsync(match.MatchId, kept, CancellationToken.None);
                throw;
            }
        });
    }

    /// <summary>
    /// Makes the match in memory the one the journal describes, and says where the journal ends. The journal
    /// is read every time: another process may have written to it since this one last looked.
    /// </summary>
    private async Task<Result<JournalPosition>> LoadAsync(JournaledMatch match, Kept kept, CancellationToken cancellationToken)
    {
        var reading = await journal.ReadAsync(match.Run, cancellationToken);
        if (kept.Position == reading.Position && await matches.FindAsync(match.MatchId, cancellationToken) is not null)
        {
            return Result.Success(reading.Position);
        }

        kept.Position = null;
        var rebuilt = await replay.RebuildAsync(match, reading.Decisions, cancellationToken);
        if (rebuilt.IsFailure)
        {
            await matches.ForgetAsync(match.MatchId, cancellationToken);
            return Result.Failure<JournalPosition>(rebuilt.Error);
        }

        kept.Position = reading.Position;
        kept.Held = rebuilt.Value;
        return Result.Success(reading.Position);
    }

    private async Task LetGoAsync(MatchId matchId, Kept kept, CancellationToken cancellationToken)
    {
        kept.Position = null;
        kept.Held = HeldAnswers.None;
        await matches.ForgetAsync(matchId, cancellationToken);
    }

    /// <summary>
    /// Whether a refusal is the stepper's answer to a decision it did not take: one the seat is not being
    /// asked, or one its options do not offer. Those are checked before anything is handed to the match.
    /// </summary>
    private static bool RefusedUntaken(DomainError error) =>
        error.Code.StartsWith(DecisionCodes, StringComparison.Ordinal);

    private static readonly string DecisionCodes = DecisionErrors.NotPending.Code[..(DecisionErrors.NotPending.Code.IndexOf('.', StringComparison.Ordinal) + 1)];

    /// <summary>What this process keeps of one match between two steps: where its journal ended, and the answers held.</summary>
    private sealed class Kept
    {
        private readonly Lock _gate = new();
        private Task _turn = Task.CompletedTask;

        /// <summary>Runs a step once the ones asked for before it are done, whether they succeeded or not.</summary>
        public Task<TResult> InTurnAsync<TResult>(Func<Task<TResult>> step)
        {
            lock (_gate)
            {
                var mine = AfterAsync(_turn, step);
                _turn = mine.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return mine;
            }

            static async Task<TResult> AfterAsync(Task turn, Func<Task<TResult>> step)
            {
                await turn;
                return await step();
            }
        }

        /// <summary>Where the journal ended when the match in memory was last in step with it; none when it is not.</summary>
        public JournalPosition? Position { get; set; }

        public HeldAnswers Held { get; set; } = HeldAnswers.None;
    }
}
