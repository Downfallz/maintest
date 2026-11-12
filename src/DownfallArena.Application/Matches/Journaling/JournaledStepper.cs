using System.Collections.Concurrent;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Journaling;

/// <summary>
/// Steps a match whose journal is the source of truth (ADR 0159). Before each step the journal is read; the
/// match in memory is used when it stands where the journal ends, taken on through the lines written since when
/// it stood where the journal ended before them, and rebuilt from the journal otherwise. What
/// a step does is written after the rest, on the condition that nobody else wrote first.
/// </summary>
/// <remarks>
/// <para>
/// The match in memory is a cache. Losing it -- a process that restarted -- costs a replay and nothing else, so a
/// caller needs nothing but the <see cref="JournaledMatch" /> between two requests. Another process writing the
/// last lines costs those lines: two processes taking turns at one table, a host and a worker, each read and
/// take what the other wrote, not the whole match again.
/// </para>
/// <para>
/// A step is applied in memory and then written. When the writing is refused, the match in memory is ahead of
/// its journal and is let go of: the caller is told the match moved (<see cref="JournalErrors.Moved" />) and
/// the next step rebuilds. One thing at a time a match: two requests for one table take turns here, since they
/// would otherwise share an aggregate.
/// </para>
/// <para>
/// Every answer carries the match as it stands, for both seats and off the same turn
/// (<see cref="JournaledState" />), so a host that keeps the last one serves a seat without coming back here.
/// </para>
/// </remarks>
/// <param name="inStep">Told, in the same turn as each of those answers, that the match stands where its journal ends.</param>
/// <param name="timeOut">What times a match out (ADR 0169); a stepper made without it cannot.</param>
public sealed class JournaledStepper(
    MatchStepper stepper,
    MatchReplay replay,
    IDecisionJournal journal,
    IMatchRepository matches,
    ICommandHandler<Concede, Result> concede,
    IEnumerable<IMatchInStep>? inStep = null,
    ICommandHandler<TimeOut, Result>? timeOut = null)
{
    private readonly IReadOnlyList<IMatchInStep> _inStep = [.. inStep ?? []];

    private static readonly string DecisionCodes = DecisionErrors.NotPending.Code[..(DecisionErrors.NotPending.Code.IndexOf('.', StringComparison.Ordinal) + 1)];

    private readonly ConcurrentDictionary<MatchId, Kept> _kept = new();

    /// <summary>The match as it stands where its journal ends.</summary>
    public Task<Result<JournaledState>> ReadAsync(JournaledMatch match, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        return InTurnAsync(match.MatchId, async kept =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            return loaded.IsFailure
                ? Result.Failure<JournaledState>(loaded.Error)
                : await StateAsync(match, kept, loaded.Value, cancellationToken);
        });
    }

    /// <summary>Takes every decision the Player agents owe, until a person is asked or the match is over.</summary>
    public Task<Result<JournaledStep>> AdvanceAsync(JournaledMatch match, MatchSeats seats, CancellationToken cancellationToken = default) =>
        AdvanceAsync(match, seats, once: false, cancellationToken);

    /// <summary>
    /// Takes the decisions the Player agents owe, all of them or one turn's worth.
    /// </summary>
    /// <param name="once">
    /// Whether to stop after one thing the agents do (<see cref="MatchStepper.AdvanceAsync(MatchId, MatchSeats, HeldAnswers, bool, CancellationToken)" />).
    /// Each such step is a turn of its own on the match, so whatever else is waiting for the match -- a decision,
    /// a seat changing hands, a reader -- has it between two of the agents' turns.
    /// </param>
    public Task<Result<JournaledStep>> AdvanceAsync(JournaledMatch match, MatchSeats seats, bool once, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seats);
        return AdvanceAsync(match, _ => seats, once, cancellationToken);
    }

    /// <summary>
    /// Takes the decisions the Player agents owe, the seats being whatever the journal's seat changes make them
    /// when the step has its turn.
    /// </summary>
    /// <param name="seated">
    /// Who sits where, given the seat changes the journal holds. Asked inside the step's turn, so a seat told to
    /// change hands while this step waited for the match has changed hands by the time the step reads it.
    /// </param>
    public Task<Result<JournaledStep>> AdvanceAsync(JournaledMatch match, Func<IReadOnlyList<SeatChangeEntry>, MatchSeats> seated, bool once, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seated);
        return AdvanceAsync(match, (changes, _) => seated(changes), once, cancellationToken);
    }

    /// <summary>
    /// Takes the decisions the Player agents owe, the seats being whatever the journal's seat changes make them
    /// when the step has its turn, and told where the journal ends then.
    /// </summary>
    /// <param name="seated">
    /// Who sits where, given the seat changes the journal holds and where the journal ends as the step begins:
    /// what an agent whose dice are thrown per decision is seeded from, so that any process taking the same turn
    /// takes it the same way (ADR 0159, phase 5).
    /// </param>
    public Task<Result<JournaledStep>> AdvanceAsync(JournaledMatch match, Func<IReadOnlyList<SeatChangeEntry>, JournalPosition, MatchSeats> seated, bool once, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(seated);
        return StepAsync(
            match,
            kept => stepper.AdvanceAsync(match.MatchId, seated([.. kept.SeatChanges], kept.Position ?? JournalPosition.Start), kept.Held, once, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Takes the decision of a seat nobody here answers for, then advances. Refused as the stepper refuses it,
    /// and with <see cref="JournalErrors.Moved" /> when another writer took the match further meanwhile.
    /// </summary>
    /// <param name="asking">
    /// Which asking of the seat's the caller believes it is answering (<see cref="SeatReading.Asking" />), or
    /// none to answer whatever the seat is asked. An answer to an asking that is no longer the seat's is late
    /// (<see cref="DecisionErrors.NotPending" />), even when the question the seat is asked now has the same shape.
    /// </param>
    public Task<Result<JournaledStep>> SubmitAsync(JournaledMatch match, MatchSeats seats, SeatDecision submitted, long? asking = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seats);
        return SubmitAsync(match, _ => seats, submitted, asking, cancellationToken);
    }

    /// <summary>
    /// Takes the decision of a seat nobody here answers for, the seats being whatever the journal's seat changes
    /// make them when the decision has its turn.
    /// </summary>
    /// <param name="seated">
    /// Who sits where, given the seat changes the journal holds; none when they leave the decision no longer the
    /// submitter's to make, which is answered as a late one (<see cref="DecisionErrors.NotPending" />).
    /// </param>
    public Task<Result<JournaledStep>> SubmitAsync(JournaledMatch match, Func<IReadOnlyList<SeatChangeEntry>, MatchSeats?> seated, SeatDecision submitted, long? asking = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(seated);
        ArgumentNullException.ThrowIfNull(submitted);
        return StepAsync(
            match,
            kept => (asking is { } believed && believed != kept.Asking(submitted.Slot)) || seated([.. kept.SeatChanges]) is not { } seats
                ? Task.FromResult(Result.Failure<MatchStep>(DecisionErrors.NotPending))
                : stepper.SubmitAsync(match.MatchId, seats, kept.Held, submitted, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// A seat gives the match up (ADR 0087): the match ends, and the journal says so, in one turn. The answers
    /// the seats were holding are nobody's any more.
    /// </summary>
    public Task<Result<JournaledState>> ConcedeAsync(JournaledMatch match, PlayerSlot slot, CancellationToken cancellationToken = default) =>
        ConcedeAsync(match, slot, _ => null, cancellationToken);

    /// <summary>
    /// A seat gives the match up, whether it may being settled against the match as it stands when the
    /// concession has its turn.
    /// </summary>
    /// <param name="refused">
    /// Why the seat may not give the match up, given the match as it stands, or nothing when it may. Asked
    /// inside the turn that ends the match, of a match still being played: whoever held the seat when the
    /// concession was asked for may not hold it any more by the time the match is the concession's.
    /// </param>
    public Task<Result<JournaledState>> ConcedeAsync(JournaledMatch match, PlayerSlot slot, Func<JournaledState, DomainError?> refused, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(refused);
        return EndAsync(
            match,
            standing => standing.Outcome is null && refused(standing) is { } refusal
                ? Result.Failure<Ending>(refusal)
                : Result.Success(new Ending(new ConcessionEntry(slot), () => concede.HandleAsync(new Concede(match.MatchId, slot), cancellationToken))),
            cancellationToken);
    }

    /// <summary>
    /// The players who let the table's time to answer run out lose the match, or draw it when both did (ADR 0169), the
    /// match and the journal in one turn, like a concession.
    /// </summary>
    /// <param name="late">
    /// Which players ran out, given the match as it stands when the timeout has its turn: who is asked then, and
    /// for how long, is the caller's to know. None, and nothing ends.
    /// </param>
    public Task<Result<JournaledState>> TimeOutAsync(JournaledMatch match, Func<JournaledState, IReadOnlyList<PlayerSlot>> late, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(late);
        var ender = timeOut ?? throw new InvalidOperationException("This stepper was made without the command that times a match out.");
        return EndAsync(
            match,
            standing =>
            {
                if (standing.Outcome is not null)
                {
                    return Result.Failure<Ending>(MatchErrors.NotInProgress);
                }

                IReadOnlyList<PlayerSlot> slots = [.. late(standing).Distinct()];
                return slots.Count == 0
                    ? Result.Failure<Ending>(MatchErrors.NobodyTimedOut)
                    : Result.Success(new Ending(new TimeoutEntry(slots), () => ender.HandleAsync(new TimeOut(match.MatchId, slots), cancellationToken)));
            },
            cancellationToken);
    }

    /// <summary>
    /// Ends the match in one turn: what ends it is settled against the match as the turn finds it, applied, and written
    /// as its line. The answers the seats were holding are nobody's any more.
    /// </summary>
    private Task<Result<JournaledState>> EndAsync(JournaledMatch match, Func<JournaledState, Result<Ending>> decide, CancellationToken cancellationToken) =>
        InTurnAsync(match.MatchId, async kept =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result.Failure<JournaledState>(loaded.Error);
            }

            var standing = await StateAsync(match, kept, loaded.Value, cancellationToken);
            if (standing.IsFailure)
            {
                return standing;
            }

            var ending = decide(standing.Value);
            if (ending.IsFailure)
            {
                return Result.Failure<JournaledState>(ending.Error);
            }

            try
            {
                // Inside what lets the match go on a failure: the command changes the match in memory before it
                // is saved and its events are handed on, so one that throws there leaves an ended match the
                // journal knows nothing of.
                var ended = await ending.Value.End();
                if (ended.IsFailure)
                {
                    return Result.Failure<JournaledState>(ended.Error);
                }

                var written = await WriteAsync(match, kept, loaded.Value, [ending.Value.Entry], cancellationToken);
                if (written is null)
                {
                    return Result.Failure<JournaledState>(JournalErrors.Moved);
                }

                kept.Held = HeldAnswers.None;
                return await StateAsync(match, kept, written, cancellationToken);
            }
            catch
            {
                // The match in memory may be ended, and the journal may not say so.
                await LetGoAsync(match.MatchId, kept, CancellationToken.None);
                throw;
            }
        });

    /// <summary>What ends a match: its line in the journal, and the command that ends it in memory.</summary>
    private sealed record Ending(IJournalEntry Entry, Func<Task<Result>> End);

    /// <summary>
    /// Writes down that a seat changes hands from the top of a round. Who the name stands for is the caller's
    /// to say: this keeps the line where the decisions are, so whoever picks the match up reads it there
    /// (<see cref="JournaledState.SeatChanges" />).
    /// </summary>
    /// <remarks>
    /// A round the match has left is refused here. The round it is in is not: a seat that has decided nothing in
    /// it yet changes hands at its first decision there, and whether it has is the caller's to know, since only
    /// the caller sees each decision's round as it is taken.
    /// </remarks>
    public Task<Result<JournaledState>> ChangeSeatAsync(JournaledMatch match, SeatChangeEntry change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        return ChangeSeatAsync(match, _ => Result.Success(change), cancellationToken);
    }

    /// <summary>
    /// Writes down that a seat changes hands, the change being settled against the match as it stands when the
    /// request has its turn.
    /// </summary>
    /// <param name="asked">
    /// The change to write, or why there is none, given the match as it stands. Asked inside the turn that writes
    /// it: whatever the caller knows of the seat is then true of the very moment the line lands after.
    /// </param>
    public Task<Result<JournaledState>> ChangeSeatAsync(JournaledMatch match, Func<JournaledState, Result<SeatChangeEntry>> asked, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(asked);
        return InTurnAsync(match.MatchId, async kept =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result.Failure<JournaledState>(loaded.Error);
            }

            var state = await StateAsync(match, kept, loaded.Value, cancellationToken);
            if (state.IsFailure)
            {
                return state;
            }

            if (state.Value.Outcome is not null)
            {
                return Result.Failure<JournaledState>(MatchErrors.NotInProgress);
            }

            var wanted = asked(state.Value);
            if (wanted.IsFailure)
            {
                return Result.Failure<JournaledState>(wanted.Error);
            }

            var change = wanted.Value;
            if (change.FromRound < (state.Value.Player1.View.Board.RoundNumber ?? 0))
            {
                return Result.Failure<JournaledState>(JournalErrors.RoundReached);
            }

            var written = await WriteAsync(match, kept, loaded.Value, [change], cancellationToken);
            if (written is null)
            {
                return Result.Failure<JournaledState>(JournalErrors.Moved);
            }

            kept.SeatChanges.Add(change);
            return Result.Success(state.Value with { Position = written, SeatChanges = [.. kept.SeatChanges] });
        });
    }

    /// <summary>
    /// Lets go of a match this process is done with. The journal is untouched, so the match is rebuilt if
    /// anybody asks for it again; a host that plays many for days would otherwise keep every one.
    /// </summary>
    public Task ForgetAsync(MatchId matchId, CancellationToken cancellationToken = default) =>
        InTurnAsync(matchId, async kept =>
        {
            await LetGoAsync(matchId, kept, cancellationToken);

            // Retired inside its own turn, and only then taken out: a step already waiting behind this one
            // finds it retired and takes its turn again with whatever is kept for the match by then.
            kept.Retired = true;
            _kept.TryRemove(new KeyValuePair<MatchId, Kept>(matchId, kept));
            return true;
        });

    private Task<Result<JournaledStep>> StepAsync(JournaledMatch match, Func<Kept, Task<Result<MatchStep>>> take, CancellationToken cancellationToken) =>
        InTurnAsync(match.MatchId, async kept =>
        {
            var loaded = await LoadAsync(match, kept, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result.Failure<JournaledStep>(loaded.Error);
            }

            try
            {
                return await TakenAsync(match, kept, loaded.Value, take, cancellationToken);
            }
            catch
            {
                // Whatever stopped the step, the match in memory may no longer be what the journal says.
                await LetGoAsync(match.MatchId, kept, CancellationToken.None);
                throw;
            }
        });

    /// <summary>A step taken on the match in memory, written after the position it was loaded at, and the match as it leaves it.</summary>
    private async Task<Result<JournaledStep>> TakenAsync(
        JournaledMatch match,
        Kept kept,
        JournalPosition loaded,
        Func<Kept, Task<Result<MatchStep>>> take,
        CancellationToken cancellationToken)
    {
        var step = await take(kept);
        if (step.IsFailure)
        {
            // A decision refused before it was taken changed nothing. Any other refusal may have come after
            // part of a step, which the journal knows nothing of.
            if (!RefusedUntaken(step.Error))
            {
                await LetGoAsync(match.MatchId, kept, cancellationToken);
            }

            return Result.Failure<JournaledStep>(step.Error);
        }

        if (await DecidedAsync(match, kept, loaded, step.Value.Decided, cancellationToken) is not { } position)
        {
            return Result.Failure<JournaledStep>(JournalErrors.Moved);
        }

        kept.Held = step.Value.Held;
        var state = await StateAsync(match, kept, position, cancellationToken);
        return state.IsFailure
            ? Result.Failure<JournaledStep>(state.Error)
            : Result.Success(new JournaledStep(step.Value, state.Value));
    }

    /// <summary>
    /// Writes what a step decided and counts it for its seats. Where the journal ends afterwards, which is where
    /// it ended before for a step that decided nothing; none when another writer got there first.
    /// </summary>
    private async Task<JournalPosition?> DecidedAsync(JournaledMatch match, Kept kept, JournalPosition after, IReadOnlyList<SeatDecision> decided, CancellationToken cancellationToken)
    {
        if (decided.Count == 0)
        {
            return after;
        }

        var written = await WriteAsync(match, kept, after, [.. decided.Select(decision => new DecisionEntry(decision))], cancellationToken);
        if (written is null)
        {
            return null;
        }

        foreach (var decision in decided)
        {
            kept.Decided[Index(decision.Slot)]++;
        }

        return written;
    }

    /// <summary>
    /// Runs one thing at a time for a match. What is kept for a match can be let go of while a caller waits
    /// for its turn on it (<see cref="ForgetAsync" />); a caller that finds it retired starts over with what is
    /// kept now, so everything asked of one match always takes turns on the same thing.
    /// </summary>
    private async Task<TResult> InTurnAsync<TResult>(MatchId matchId, Func<Kept, Task<TResult>> step)
    {
        while (true)
        {
            var kept = _kept.GetOrAdd(matchId, _ => new Kept());
            var (ran, result) = await kept.InTurnAsync(async () => kept.Retired ? (false, default(TResult)!) : (true, await step(kept)));
            if (ran)
            {
                return result;
            }
        }
    }

    /// <summary>
    /// Makes the match in memory the one the journal describes, and says where the journal ends. The journal
    /// is read every time: another process may have written to it since this one last looked. A match in memory
    /// that stood where the journal ended is taken on through the lines written since, and only those are read
    /// (inventory question 13); any other is rebuilt from the whole journal.
    /// </summary>
    private async Task<Result<JournalPosition>> LoadAsync(JournaledMatch match, Kept kept, CancellationToken cancellationToken)
    {
        // Before anything is read or rebuilt: a step that reaches its turn after its caller gave the match up
        // must not bring back a match that was let go of while it waited.
        cancellationToken.ThrowIfCancellationRequested();
        if (kept.Position is { } known && await matches.FindAsync(match.MatchId, cancellationToken) is not null
            && await journal.ReadAfterAsync(match.Run, known, cancellationToken) is { } since)
        {
            if (since.Position == known)
            {
                return Result.Success(known);
            }

            if (await ContinuedAsync(match, kept, since, cancellationToken))
            {
                return Result.Success(since.Position);
            }
        }

        kept.Position = null;
        var reading = await journal.ReadAsync(match.Run, cancellationToken);
        Replaying(match, [.. reading.Entries.OfType<SeatChangeEntry>()]);
        var rebuilt = await replay.RebuildAsync(match, reading.Entries, cancellationToken);
        if (rebuilt.IsFailure)
        {
            await matches.ForgetAsync(match.MatchId, cancellationToken);
            return Result.Failure<JournalPosition>(rebuilt.Error);
        }

        kept.Position = reading.Position;
        kept.Held = rebuilt.Value;
        kept.Entries = reading.Entries.Count;
        kept.SeatChanges.Clear();
        kept.Decided[0] = kept.Decided[1] = 0;
        Count(kept, reading.Entries);
        return Result.Success(reading.Position);
    }

    /// <summary>
    /// Takes the match in memory on through the lines another process wrote since it last stood where the
    /// journal ended. Whether it now stands where the journal ends; when it does not, it went part of the way
    /// and is rebuilt, which also names the line it stopped at.
    /// </summary>
    private async Task<bool> ContinuedAsync(JournaledMatch match, Kept kept, JournalReading since, CancellationToken cancellationToken)
    {
        // Out of step until the lines are all taken: a step that throws halfway leaves a match no position describes.
        kept.Position = null;
        Replaying(match, [.. kept.SeatChanges, .. since.Entries.OfType<SeatChangeEntry>()]);
        var continued = await replay.ContinueAsync(match, kept.Held, since.Entries, kept.Entries, cancellationToken);
        if (continued.IsFailure)
        {
            return false;
        }

        kept.Position = since.Position;
        kept.Held = continued.Value;
        kept.Entries += since.Entries.Count;
        Count(kept, since.Entries);
        return true;
    }

    private void Replaying(JournaledMatch match, IReadOnlyList<SeatChangeEntry> seatChanges)
    {
        foreach (var told in _inStep)
        {
            told.Replaying(match.MatchId, seatChanges);
        }
    }

    /// <summary>Counts entries the match in memory has gone through: each seat's decisions and the seat changes.</summary>
    private static void Count(Kept kept, IReadOnlyList<IJournalEntry> entries)
    {
        kept.SeatChanges.AddRange(entries.OfType<SeatChangeEntry>());
        foreach (var entry in entries.OfType<DecisionEntry>())
        {
            kept.Decided[Index(entry.Decision.Slot)]++;
        }
    }

    /// <summary>
    /// Writes entries after the position the turn loaded at. Refused, the match in memory is ahead of its
    /// journal -- or simply not where the journal is -- and is let go of.
    /// </summary>
    private async Task<JournalPosition?> WriteAsync(JournaledMatch match, Kept kept, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken)
    {
        var written = await journal.AppendAsync(match.Run, after, entries, cancellationToken);
        if (written is null)
        {
            await LetGoAsync(match.MatchId, kept, cancellationToken);
            return null;
        }

        kept.Position = written;
        kept.Entries += entries.Count;
        return written;
    }

    private async Task<Result<JournaledState>> StateAsync(JournaledMatch match, Kept kept, JournalPosition position, CancellationToken cancellationToken)
    {
        var readings = new List<SeatReading>(2);
        foreach (var slot in new[] { PlayerSlot.Player1, PlayerSlot.Player2 })
        {
            var view = await stepper.SeatAsync(match.MatchId, slot, kept.Held, cancellationToken);
            if (view.IsFailure)
            {
                return Result.Failure<JournaledState>(view.Error);
            }

            readings.Add(new SeatReading(view.Value, kept.Asking(slot)));
        }

        // Every state is read off a match that stands where the journal ends: loaded, rebuilt or just written.
        foreach (var told in _inStep)
        {
            told.InStep(match.MatchId, position);
        }

        return Result.Success(new JournaledState(readings[0].View.Board.Outcome, readings[0], readings[1], position, [.. kept.SeatChanges]));
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

    private static int Index(PlayerSlot slot) => slot == PlayerSlot.Player1 ? 0 : 1;

    /// <summary>What this process keeps of one match between two steps.</summary>
    private sealed class Kept
    {
        private readonly Lock _gate = new();
        private Task _turn = Task.CompletedTask;

        /// <summary>Where the journal ended when the match in memory was last in step with it; none when it is not.</summary>
        public JournalPosition? Position { get; set; }

        public HeldAnswers Held { get; set; } = HeldAnswers.None;

        /// <summary>How many decisions each seat has in the journal, seat 1 first.</summary>
        public long[] Decided { get; } = [0, 0];

        /// <summary>How many entries of the journal the match in memory has gone through, whatever their kind.</summary>
        public int Entries { get; set; }

        public List<SeatChangeEntry> SeatChanges { get; } = [];

        /// <summary>Whether this has been let go of: set and read only by whoever has the turn.</summary>
        public bool Retired { get; set; }

        /// <summary>Which asking of a seat's its next question is.</summary>
        public long Asking(PlayerSlot slot) => Decided[Index(slot)] + 1;

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
                // Handed back to the caller before anything runs. A step whose turn has come would otherwise
                // start right here, under the lock that orders the turns, and keep it for as long as a Player
                // agent thinks: everybody asking for the match meanwhile would be a thread stopped at that
                // lock rather than a step waiting its turn.
                await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                await turn;
                return await step();
            }
        }
    }
}
