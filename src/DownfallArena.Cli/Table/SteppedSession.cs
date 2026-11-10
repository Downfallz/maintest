using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Cli.Table;

/// <summary>
/// A table's match stepped over its journal (ADR 0159): nothing waits on a person. A decision is submitted,
/// written and applied in one turn of the match; what the bots owe after it is taken in the background, one of
/// their turns at a time; and everything a seat reads is the match as the last step left it.
/// </summary>
/// <remarks>
/// <para>
/// What this keeps is a cache and a courtesy. The match as it stood after the last step is kept so a read is not
/// a step; who sits where is worked out again from the table's opening and the journal's seat changes; and the
/// last round each seat decided in is noted as decisions pass, replayed ones among them, since that is what
/// says whether a seat can still change hands from the top of a round.
/// </para>
/// <para>
/// The table's version is still counted here (ADR 0119): a question asked is said to the seat's person, and
/// the trace and the outcome move it as they did. Deriving it from the journal is the next slice's.
/// </para>
/// </remarks>
internal sealed class SteppedSession : ITableSession
{
    private readonly SteppedHost _host;
    private readonly JournaledStepper _stepper;
    private readonly JournaledMatch _match;
    private readonly SteppedSeats _seats;
    private readonly PlaytestRun? _run;
    private readonly bool _legacyStream;
    private readonly CancellationToken _stopping;
    private readonly CancellationTokenRegistration _stopped;
    private readonly TaskCompletionSource<Result<MatchOutcome>> _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // What the readers are served from, and who the plan says sits where: replaced, never changed in place.
    private readonly Lock _gate = new();
    private JournaledState? _state;
    private SeatPlan _plan;
    private List<SeatChangeEntry> _folded = [];
    private (SeatChangeEntry Change, Occupant Next)? _offered;
    private readonly long[] _announced = [0, 0];

    // The last round each seat decided anything in, seat 1 first. Noted inside the match's turn.
    private readonly int[] _reached = [0, 0];

    private int _begun;
    private int _pump;
    private int _disposed;

    private SteppedSession(SteppedHost host, IDecisionJournal journal, JournaledMatch match, SteppedSeats seats, PlaytestRun? run, bool legacyStream, CancellationToken stopping)
    {
        _host = host;
        _match = match;
        _seats = seats;
        _plan = seats.Plan;
        _run = run;
        _legacyStream = legacyStream;
        _stopping = stopping;
        var seen = new Seen(this);
        _stepper = host.Stepper(journal, seen, seen);
        foreach (var seat in seats.Seats)
        {
            (seat.Person as SteppedPerson)?.Attach(() => Waiting(seat.Slot));
        }

        // A table let go of before its match ended has no outcome, which is what an abandoned table is.
        _stopped = stopping.Register(() => _outcome.TrySetCanceled());
    }

    /// <summary>
    /// Opens the table's match where its journal ends: created for a journal that holds nothing, rebuilt from
    /// it otherwise. Refused when the journal no longer answers the match it rebuilds
    /// (<see cref="JournalErrors.Diverged" />). Nobody is asked anything until the table is told to begin.
    /// </summary>
    /// <param name="legacyStream">Whether the table's feed is the one recorded before ordinary energy was an event (ADR 0132).</param>
    /// <param name="stopping">Cancelled when the table is let go of.</param>
    public static async Task<Result<SteppedSession>> OpenAsync(
        SteppedHost host,
        IDecisionJournal journal,
        JournaledMatch match,
        SteppedSeats seats,
        PlaytestRun? run = null,
        bool legacyStream = false,
        CancellationToken stopping = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(seats);

        var session = new SteppedSession(host, journal, match, seats, run, legacyStream, stopping);
        try
        {
            // Read once before the match is: a rebuild names every decision's seat as it replays it, and the
            // names come from the seat changes that are further down the same journal.
            var reading = await journal.ReadAsync(match.Run, stopping);
            session.Seated([.. reading.Entries.OfType<SeatChangeEntry>()]);
            var state = await session._stepper.ReadAsync(match, stopping);
            if (state.IsFailure)
            {
                session.Dispose();
                return Result.Failure<SteppedSession>(state.Error);
            }

            session.Take(state.Value);
            return Result.Success(session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public MatchId MatchId => _match.MatchId;

    public RuleSet Rules => _match.Rules;

    public Task<Result<MatchOutcome>> Outcome => _outcome.Task;

    public bool IsOver => _outcome.Task.IsCompleted;

    public bool HasBegun => Volatile.Read(ref _begun) != 0;

    public void Begin()
    {
        if (Interlocked.Exchange(ref _begun, 1) != 0)
        {
            return;
        }

        Announce();
        Pump();
    }

    /// <remarks>
    /// The match in memory is let go of once whatever step is being taken has finished, and its feed after it:
    /// a step still running would otherwise record into a feed nobody is left to forget.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopped.Dispose();
        _outcome.TrySetCanceled();
        _stepper.ForgetAsync(_match.MatchId).ContinueWith(
            _ => _host.Events.Forget(_match.MatchId),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public string SeatedName(PlayerSlot slot) => Sitting(slot).Name;

    public bool PlayedByItsPerson(PlayerSlot slot) => SeatPlan.IsPerson(Sitting(slot));

    public Swap? Pending(PlayerSlot slot)
    {
        var (state, plan) = Read();
        return plan.Pending(slot, RoundOf(state, slot));
    }

    /// <remarks>
    /// Settled inside the match's own turn: the round the seat has reached, who holds it and the line that
    /// says it changes hands are of one moment, so no decision of the seat's can land between the reading and
    /// the writing and leave a round split between two occupants.
    /// </remarks>
    public async Task<SwapOutcome> SwapAsync(PlayerSlot slot, Occupant next, string wanted, int round)
    {
        ArgumentNullException.ThrowIfNull(next);
        while (true)
        {
            Occupant? held = null;
            var reached = 0;
            var changed = await _stepper.ChangeSeatAsync(
                _match,
                state =>
                {
                    var plan = Seated(state.SeatChanges);
                    held = plan.At(slot, RoundOf(state, slot));
                    reached = Reached(slot, state, plan);
                    if (round <= reached)
                    {
                        return Result.Failure<SeatChangeEntry>(JournalErrors.RoundReached);
                    }

                    var change = new SeatChangeEntry(slot, wanted, round, reached);
                    lock (_gate)
                    {
                        _offered = (change, next);
                    }

                    return Result.Success(change);
                },
                _stopping);
            if (changed.IsFailure && changed.Error == JournalErrors.Moved)
            {
                continue;
            }

            held ??= Sitting(slot);
            if (changed.IsFailure)
            {
                return new SwapOutcome(held, reached == 0 ? null : reached, Taken: false);
            }

            Take(changed.Value);
            Pump();
            return new SwapOutcome(held, reached == 0 ? null : reached, Taken: true);
        }
    }

    /// <remarks>The board the last step left, the seat's held answers on it: where the match has got to, and how it ended.</remarks>
    public Task<Result<PlayerBoardState>> BoardAsync(PlayerSlot slot) =>
        Task.FromResult(Result.Success(Read().State.Of(slot).View.Board));

    /// <remarks>
    /// One moment by construction: the question, the board and the options are the ones a single step left. A
    /// read that finds the bots owing a turn nobody is taking -- a write that failed under the last one -- sets
    /// it off again, which is all a read ever does to the match.
    /// </remarks>
    public Task<Result<SeatSnapshot>> SnapshotAsync(PlayerSlot slot)
    {
        var (state, plan) = Read();
        if (HasBegun && state.Outcome is null && Waiting(PlayerSlot.Player1, state, plan) is null && Waiting(PlayerSlot.Player2, state, plan) is null)
        {
            Pump();
        }

        var view = state.Of(slot).View;
        return Task.FromResult(Result.Success(new SeatSnapshot(
            Waiting(slot, state, plan),
            view.Board,
            view.Options,
            SeatSnapshot.StillFaceDown(state.Of(Other(slot)).View.Board))));
    }

    /// <remarks>
    /// Written and applied before this answers, in the match's turn; the asking is checked there, against the
    /// journal. What the bots owe after it is not waited for: it is taken in the background, and the pages are
    /// told as it lands.
    /// </remarks>
    public async Task<DecisionTaken> DecideAsync(PlayerSlot slot, PlayerDecision decision, long asked, PlayerOptions offered, int? round)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var reading = Read().State.Of(slot);
        if (!HasBegun || reading.Asking != asked)
        {
            return DecisionTaken.Late;
        }

        // The asking is the one this session read, so the round its board says is the round of the question.
        var at = reading.View.Board.RoundNumber;
        Result<JournaledStep> step;
        try
        {
            step = await _stepper.SubmitAsync(
                _match,
                changes => SeatPlan.IsPerson(Seated(changes).At(slot, at)) ? MatchSeats.People : null,
                new SeatDecision(slot, decision),
                asked,
                _stopping);
        }
        catch (OperationCanceledException)
        {
            return DecisionTaken.Late;
        }

        if (step.IsFailure)
        {
            Stopped(step.Error);
            return DecisionTaken.Late;
        }

        Take(step.Value.State);
        Pump();

        // Held, the answer has set nothing off yet: it reaches the match with the other seat's (ADR 0111, ADR 0117).
        return new DecisionTaken(true, step.Value.Step.Held.IsEmpty);
    }

    public async Task<Result> ConcedeAsync(PlayerSlot slot)
    {
        while (true)
        {
            var conceded = await _stepper.ConcedeAsync(_match, slot, CancellationToken.None);
            if (conceded.IsSuccess)
            {
                Take(conceded.Value);
                return Result.Success();
            }

            if (conceded.Error != JournalErrors.Moved)
            {
                return Result.Failure(conceded.Error);
            }
        }
    }

    /// <remarks>Nothing to wait for: a decision is applied before <see cref="DecideAsync" /> answers.</remarks>
    public Task SettledAsync(PlayerSlot slot, int traced) => Task.CompletedTask;

    /// <summary>What a seat's person is being asked, or nothing: before the table begins, once it is over, and while a bot holds the seat.</summary>
    private Asking? Waiting(PlayerSlot slot)
    {
        var (state, plan) = Read();
        return Waiting(slot, state, plan);
    }

    private Asking? Waiting(PlayerSlot slot, JournaledState state, SeatPlan plan)
    {
        if (!HasBegun || state.Outcome is not null)
        {
            return null;
        }

        var reading = state.Of(slot);
        return reading.View.Asked is { } asked && SeatPlan.IsPerson(plan.At(slot, reading.View.Board.RoundNumber))
            ? new Asking(asked.Kind, asked.Creature, reading.Asking)
            : null;
    }

    private Occupant Sitting(PlayerSlot slot)
    {
        var (state, plan) = Read();
        return plan.At(slot, RoundOf(state, slot));
    }

    private (JournaledState State, SeatPlan Plan) Read()
    {
        lock (_gate)
        {
            return (_state ?? throw new InvalidOperationException("The table's match has not been opened."), _plan);
        }
    }

    /// <summary>Who sits where as far as the journal's seat changes have been read, which is before the match is, when it is opened.</summary>
    private SeatPlan Plan
    {
        get
        {
            lock (_gate)
            {
                return _plan;
            }
        }
    }

    private static int? RoundOf(JournaledState state, PlayerSlot slot) => state.Of(slot).View.Board.RoundNumber;

    /// <summary>
    /// The last round a seat is committed to: one it decided in, the one its person is being asked about, and
    /// every round the match has left, whoever sat through it.
    /// </summary>
    private int Reached(PlayerSlot slot, JournaledState state, SeatPlan plan)
    {
        var round = RoundOf(state, slot) ?? 0;
        var asked = Waiting(slot, state, plan) is null ? 0 : round;
        return Math.Max(Math.Max(Volatile.Read(ref _reached[Index(slot)]), asked), round - 1);
    }

    /// <summary>
    /// Who sits where, the journal's seat changes holding what they hold: the plan the table opened with and
    /// each change after it, in order. Changes already folded keep the occupants they were given -- a bot is
    /// made once and keeps its draws -- and a reading older than the last one changes nothing.
    /// </summary>
    private SeatPlan Seated(IReadOnlyList<SeatChangeEntry> changes)
    {
        lock (_gate)
        {
            if (changes.Count < _folded.Count)
            {
                return _plan;
            }

            if (!_folded.SequenceEqual(changes.Take(_folded.Count)))
            {
                _plan = _seats.Plan;
                _folded = [];
            }

            foreach (var change in changes.Skip(_folded.Count).ToList())
            {
                var next = _offered is { } offered && offered.Change == change
                    ? offered.Next
                    : _seats.Named(change.Slot, change.To) ?? throw new InvalidOperationException($"Nobody called '{change.To}' can sit in {TableSeat.NameOf(change.Slot)} any more, as the table's journal says somebody did.");
                _plan = _plan.From(change.Slot, change.FromRound, next, change.Reached);
                _folded.Add(change);
            }

            return _plan;
        }
    }

    /// <summary>Keeps the match as a step left it, unless a later step's is already kept, and says what it changed.</summary>
    private void Take(JournaledState state)
    {
        Seated(state.SeatChanges);
        lock (_gate)
        {
            if (_state is null || state.Position.Lines >= _state.Position.Lines)
            {
                _state = state;
            }
        }

        if (state.Outcome is { } outcome)
        {
            _outcome.TrySetResult(Result.Success(outcome));
        }

        Announce();
    }

    /// <summary>
    /// Says to each seat's person what they have just been asked: once an asking, whatever is read how often.
    /// The round goes with a question asked of the player for a round, and not with one a creature's slot asks.
    /// </summary>
    private void Announce()
    {
        var told = new List<(SteppedPerson Person, Asking Asking, int? Round)>(2);
        lock (_gate)
        {
            if (_state is not { } state)
            {
                return;
            }

            foreach (var seat in _seats.Seats)
            {
                var asking = Waiting(seat.Slot, state, _plan);
                var number = asking?.Asked ?? 0;
                if (asking is not null && number != _announced[Index(seat.Slot)] && seat.Person is SteppedPerson person)
                {
                    var together = asking.Kind is PlayerOptionsKind.Evolution or PlayerOptionsKind.Speed or PlayerOptionsKind.Intent;
                    told.Add((person, asking, together ? RoundOf(state, seat.Slot) : null));
                }

                _announced[Index(seat.Slot)] = number;
            }
        }

        foreach (var (person, asking, round) in told)
        {
            person.Ask(asking, round);
        }
    }

    /// <summary>
    /// Sets the bots' turns off, unless they are being taken: then whoever is taking them looks once more
    /// when done, since what was asked for may have come after its last look.
    /// </summary>
    private void Pump()
    {
        if (Interlocked.Exchange(ref _pump, 2) == 0)
        {
            _ = Task.Run(PumpAsync);
        }
    }

    private async Task PumpAsync()
    {
        do
        {
            Volatile.Write(ref _pump, 1);
            await AdvancedAsync();
        }
        while (Interlocked.CompareExchange(ref _pump, 0, 1) != 1);
    }

    /// <summary>
    /// Takes what the bots owe, one of their turns a turn of the match, until a person is asked or the match
    /// is over. Each is a turn of its own so a tap, a concession or a seat changing hands has the match between
    /// two of them, and so the pages see each as it lands.
    /// </summary>
    private async Task AdvancedAsync()
    {
        try
        {
            while (HasBegun && !IsOver && Volatile.Read(ref _disposed) == 0)
            {
                var step = await _stepper.AdvanceAsync(_match, changes => Seated(changes).Seats(), once: true, _stopping);
                if (step.IsFailure)
                {
                    if (step.Error == JournalErrors.Moved)
                    {
                        continue;
                    }

                    Stopped(step.Error);
                    return;
                }

                Take(step.Value.State);
                if (!step.Value.Step.Owed)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The table was let go of under a bot's turn.
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or Azure.RequestFailedException)
        {
            // The journal could not be written: the turn is nobody's, and the next read of a seat takes it again.
            Console.WriteLine($"  Session {_match.Run}: the bots' turn could not be written down ({failure.Message}); the next read takes it again.");
        }
        catch (Exception failure)
        {
            // A bot that throws, a match that cannot be played on: the table ends without an outcome and says why,
            // as the driver's task did.
            _outcome.TrySetException(failure);
        }
    }

    /// <summary>
    /// A step was refused. A late answer and a match already over are answers; a journal that no longer fits
    /// the match is the table's end, without an outcome.
    /// </summary>
    private void Stopped(DomainError error)
    {
        if (error.Code == JournalErrors.DivergedCode)
        {
            _outcome.TrySetResult(Result.Failure<MatchOutcome>(error));
        }
    }

    private void Reach(PlayerSlot slot, int? round)
    {
        if (round is { } number && number > Volatile.Read(ref _reached[Index(slot)]))
        {
            Volatile.Write(ref _reached[Index(slot)], number);
        }
    }

    private static int Index(PlayerSlot slot) => slot == PlayerSlot.Player1 ? 0 : 1;

    private static PlayerSlot Other(PlayerSlot slot) => slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

    /// <summary>
    /// What the table's stepper shows every decision to, and tells to forget the match before it rebuilds it.
    /// Nothing sits between a stepped match and its seats, so this is where a decision is recorded as a step
    /// under the name of whoever holds the seat (<see cref="PlaytestRun.Witnessed" />), and where its round is noted.
    /// </summary>
    private sealed class Seen(SteppedSession session) : IDecisionWitness, IMatchMemory
    {
        public IPlayerAgent Around(MatchId matchId, PlayerSlot slot, IPlayerAgent decider)
        {
            var reaching = new ReachingAgent(decider, round => session.Reach(slot, round));
            return session._run is { } run
                ? run.Witnessed(matchId, reaching, board => session.Plan.At(board.Slot, board.RoundNumber).Name)
                : reaching;
        }

        /// <remarks>
        /// The feed is forgotten here and nowhere else, so a table whose feed was recorded before ordinary
        /// energy was an event (ADR 0132) says so again before the rebuild records anything.
        /// </remarks>
        public void Forget(MatchId matchId)
        {
            Volatile.Write(ref session._reached[0], 0);
            Volatile.Write(ref session._reached[1], 0);
            session._run?.Forget(matchId);
            session._host.Events.Forget(matchId);
            if (session._legacyStream)
            {
                session._host.Events.UseLegacyEventStream(matchId);
            }
        }
    }
}
