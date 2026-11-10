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

    // Whether the match kept here may be behind its journal: a step was refused or failed, and what it left
    // is not known. Whoever takes the bots' turns reads the match again first.
    private int _stale;

    private static readonly TimeSpan FirstRetry = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(30);

    // How often in a row another writer may take the match further before the step waits a moment, or gives up
    // where somebody is waiting for its answer.
    private const int MovedInARow = 3;

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

        _stopped = stopping.Register(Stop);
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
        Result<JournaledState> state;
        try
        {
            // Read once before the match is: a rebuild names every decision's seat as it replays it, and the
            // names come from the seat changes that are further down the same journal.
            var reading = await journal.ReadAsync(match.Run, stopping);
            session.Seated([.. reading.Entries.OfType<SeatChangeEntry>()]);
            state = await session._stepper.ReadAsync(match, stopping);
        }
        catch
        {
            session.Stop();
            throw;
        }

        if (state.IsFailure)
        {
            session.Stop();
            return Result.Failure<SteppedSession>(state.Error);
        }

        session.Take(state.Value);
        return Result.Success(session);
    }

    /// <remarks>
    /// Raised once a step's match is what the seats read. The feed says an entry was recorded while the step
    /// that recorded it is still being taken; a page that read on that word alone would have the new entries
    /// beside the board of before, and nothing to tell it to read again.
    /// </remarks>
    public event Action? Changed;

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

    public void Dispose()
    {
        _stopped.Dispose();
        Stop();
    }

    /// <summary>
    /// Lets the table go: nothing more is taken, the match in memory and its feed are forgotten once whatever
    /// step is being taken has finished, and only then does a match that had not ended complete without an
    /// outcome, which is what an abandoned table is. Whoever waits for the outcome before deleting the table's
    /// files is then waiting for the last line a step could write.
    /// </summary>
    private void Stop()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = LetGoAsync();
        }
    }

    private async Task LetGoAsync()
    {
        try
        {
            // Not the table's own token, which is cancelled by now: this is what lets the match go.
            await _stepper.ForgetAsync(_match.MatchId, CancellationToken.None);
        }
        finally
        {
            _host.Events.Forget(_match.MatchId);
            _outcome.TrySetCanceled(_stopping);
        }
    }

    private bool LetGo => Volatile.Read(ref _disposed) != 0;

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
        var asked = new SwapAsking(slot, next, wanted, round);
        for (var attempt = 0; attempt < MovedInARow && !LetGo; attempt++)
        {
            var changed = await ChangedAsync(asked);
            if (changed is not { } answered)
            {
                break;
            }

            if (answered.IsSuccess)
            {
                Take(answered.Value);
                Pump();
                return asked.Outcome(Sitting(slot), taken: true);
            }

            if (answered.Error != JournalErrors.Moved)
            {
                // Refused on a match that is over, the caller is about to ask whether it is.
                await CaughtUpAsync();
                break;
            }
        }

        Behind();
        return asked.Outcome(Sitting(slot), taken: false);
    }

    /// <summary>The seat change asked for, taken to the match's turn; none when the table was let go of meanwhile.</summary>
    private async Task<Result<JournaledState>?> ChangedAsync(SwapAsking asked)
    {
        try
        {
            return await _stepper.ChangeSeatAsync(_match, state => Settled(asked, state), _stopping);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            Behind();
            throw;
        }
    }

    /// <summary>
    /// The line for a seat change, or why there is none, against the match as its turn finds it: who holds the
    /// seat and the last round it is committed to are read here, where nothing of the seat's can land after.
    /// </summary>
    private Result<SeatChangeEntry> Settled(SwapAsking asked, JournaledState state)
    {
        var plan = Seated(state.SeatChanges);
        asked.Held = plan.At(asked.Slot, RoundOf(state, asked.Slot));
        asked.Reached = Reached(asked.Slot, state, plan);
        if (asked.Round <= asked.Reached)
        {
            return Result.Failure<SeatChangeEntry>(JournalErrors.RoundReached);
        }

        var change = new SeatChangeEntry(asked.Slot, asked.Wanted, asked.Round, asked.Reached);
        lock (_gate)
        {
            _offered = (change, asked.Next);
        }

        return Result.Success(change);
    }

    /// <summary>A seat change being asked for, and what its turn on the match found of the seat.</summary>
    private sealed class SwapAsking(PlayerSlot slot, Occupant next, string wanted, int round)
    {
        public PlayerSlot Slot { get; } = slot;

        public Occupant Next { get; } = next;

        public string Wanted { get; } = wanted;

        public int Round { get; } = round;

        /// <summary>Who held the seat when the change had its turn; none when it never had one.</summary>
        public Occupant? Held { get; set; }

        public int Reached { get; set; }

        public SwapOutcome Outcome(Occupant sitting, bool taken) => new(Held ?? sitting, Reached == 0 ? null : Reached, taken);
    }

    /// <remarks>The board the last step left, the seat's held answers on it: where the match has got to, and how it ended.</remarks>
    public Task<Result<PlayerBoardState>> BoardAsync(PlayerSlot slot) =>
        Task.FromResult(Result.Success(Read().State.Of(slot).View.Board));

    /// <remarks>
    /// One moment by construction: the question, the board and the options are the ones a single step left. A
    /// read that finds the match kept here possibly behind its journal, or the bots owing a turn, sets off
    /// whoever takes those turns, which is all a read ever does to the match.
    /// </remarks>
    public Task<Result<SeatSnapshot>> SnapshotAsync(PlayerSlot slot)
    {
        var (state, plan) = Read();
        if (Volatile.Read(ref _stale) != 0 || (HasBegun && state.Outcome is null && Waiting(PlayerSlot.Player1, state, plan) is null && Waiting(PlayerSlot.Player2, state, plan) is null))
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
        if (LetGo || !HasBegun || reading.Asking != asked)
        {
            return DecisionTaken.Late;
        }

        // The asking is the one this session read, so the round its board says is the round of the question.
        var at = reading.View.Board.RoundNumber;
        var step = Result.Failure<JournaledStep>(JournalErrors.Moved);
        for (var attempt = 0; attempt < MovedInARow && step.IsFailure && step.Error == JournalErrors.Moved; attempt++)
        {
            try
            {
                // Taken again when another writer got in the way: the asking says whether it is still this
                // decision's to answer, and a line of its own found already written makes it a late one.
                step = await _stepper.SubmitAsync(
                    _match,
                    changes => SeatPlan.IsPerson(Seated(changes).At(slot, at)) ? MatchSeats.People : null,
                    new SeatDecision(slot, decision),
                    asked,
                    _stopping);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                return DecisionTaken.Late;
            }
            catch
            {
                // Written or not, nobody here knows: the line may be in the journal with its answer lost on the way.
                Behind();
                throw;
            }
        }

        if (step.IsFailure)
        {
            // Late for the match as it is kept here, which may itself be what is late.
            Behind();
            return DecisionTaken.Late;
        }

        Take(step.Value.State);
        Pump();

        // Held, the answer has set nothing off yet: it reaches the match with the other seat's (ADR 0111, ADR 0117).
        return new DecisionTaken(true, step.Value.Step.Held.IsEmpty);
    }

    /// <remarks>
    /// A concession another writer got in the way of is taken again. When it is then refused because the match
    /// is over by this very seat's concession, the line was its own, written by the attempt whose answer was
    /// lost: the match is conceded. A concession refused outright on a match that is over stays refused.
    /// </remarks>
    public async Task<Result> ConcedeAsync(PlayerSlot slot)
    {
        var refusal = JournalErrors.Moved;
        var moved = false;
        for (var attempt = 0; attempt < MovedInARow && !LetGo; attempt++)
        {
            Result<JournaledState> conceded;
            try
            {
                conceded = await _stepper.ConcedeAsync(_match, slot, _stopping);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                Behind();
                throw;
            }

            if (conceded.IsSuccess)
            {
                Take(conceded.Value);
                return Result.Success();
            }

            refusal = conceded.Error;
            if (refusal != JournalErrors.Moved)
            {
                break;
            }

            moved = true;
        }

        var ended = await CaughtUpAsync();
        return moved && ended is { Outcome: { Reason: MatchEndReason.Concession } outcome } && outcome.Winner == Other(slot)
            ? Result.Success()
            : Result.Failure(refusal);
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
        var kept = false;
        lock (_gate)
        {
            if (_state is null || state.Position.Lines >= _state.Position.Lines)
            {
                kept = true;
                _state = state;
            }
        }

        if (state.Outcome is { } outcome)
        {
            _outcome.TrySetResult(Result.Success(outcome));
        }

        Announce();
        if (kept)
        {
            Changed?.Invoke();
        }
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
            _ = Task.Run(TurnsAsync, CancellationToken.None);
        }
    }

    private async Task TurnsAsync()
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
    /// <remarks>
    /// It is also where the match kept here is read again when it may be behind its journal, and what does not
    /// give up on a store that is out: a turn that could not be written is taken again after a wait that grows
    /// to half a minute, for as long as the table is held. Nobody may be reading the table to set it off again
    /// -- a page closed on the bot's turn is the very case a notice exists for (ADR 0118).
    /// </remarks>
    private async Task AdvancedAsync()
    {
        var wait = FirstRetry;
        var moved = 0;
        while (!LetGo)
        {
            Turn turn;
            try
            {
                turn = await TurnAsync();
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // The table was let go of under a bot's turn.
                return;
            }
            catch (Exception failure) when (Transient(failure))
            {
                Behind(pump: false);
                Console.WriteLine($"  Session {_match.Run}: the bots' turn could not be taken ({failure.Message}); it is taken again in {wait.TotalSeconds:0.##} s.");
                turn = Turn.Failed;
            }
            catch (Exception failure)
            {
                // A bot that throws, a journal that cannot be read as one: the table ends without an outcome,
                // as the driver's task did, and says why.
                Console.WriteLine($"  Session {_match.Run} cannot be played on: {failure.Message}");
                _outcome.TrySetException(failure);
                return;
            }

            if (turn == Turn.Done)
            {
                return;
            }

            if (turn == Turn.Taken)
            {
                moved = 0;
                wait = FirstRetry;
            }
            else if (turn == Turn.Failed || ++moved >= MovedInARow)
            {
                // The store is out, or another writer keeps taking the match further: not at once again.
                if (await WaitedAsync(wait) is not { } next)
                {
                    return;
                }

                wait = next;
            }
        }
    }

    /// <summary>
    /// One turn of whoever takes the bots' turns: the match read again when it may be behind its journal, then
    /// one of the agents' turns taken.
    /// </summary>
    private async Task<Turn> TurnAsync()
    {
        if (Interlocked.Exchange(ref _stale, 0) != 0)
        {
            var read = await _stepper.ReadAsync(_match, _stopping);
            if (read.IsFailure)
            {
                Ended(read.Error);
                return Turn.Done;
            }

            Take(read.Value);
        }

        if (!HasBegun || IsOver)
        {
            return Turn.Done;
        }

        var step = await _stepper.AdvanceAsync(_match, changes => Seated(changes).Seats(), once: true, _stopping);
        if (step.IsFailure && step.Error == JournalErrors.Moved)
        {
            // Another writer took the match further: it is read again before the next turn.
            Behind(pump: false);
            return Turn.Moved;
        }

        if (step.IsFailure)
        {
            Ended(step.Error);
            return Turn.Done;
        }

        Take(step.Value.State);
        return step.Value.Step.Owed ? Turn.Taken : Turn.Done;
    }

    /// <summary>How a turn of whoever takes the bots' turns went.</summary>
    private enum Turn
    {
        /// <summary>Nothing more is owed: a person is asked, the match is over, or the table cannot be played on.</summary>
        Done,

        /// <summary>A turn was taken and the agents owe another.</summary>
        Taken,

        /// <summary>Another writer took the match further first.</summary>
        Moved,

        /// <summary>The store could not be reached.</summary>
        Failed,
    }

    /// <summary>Waits before a turn is taken again, and says how long the next wait is; nothing when the table was let go of meanwhile.</summary>
    private async Task<TimeSpan?> WaitedAsync(TimeSpan wait)
    {
        try
        {
            await Task.Delay(wait, _host.Clock, _stopping);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return wait + wait < LongestRetry ? wait + wait : LongestRetry;
    }

    /// <summary>
    /// Whether a failure is the store's and may pass: a file or a blob that could not be reached, a request that
    /// timed out. Anything else is the table's own, and is not taken again.
    /// </summary>
    private static bool Transient(Exception failure) => failure switch
    {
        AggregateException several => several.InnerExceptions.Count > 0 && several.InnerExceptions.All(Transient),
        _ => failure is IOException or UnauthorizedAccessException or TimeoutException or OperationCanceledException or Azure.RequestFailedException,
    };

    /// <summary>
    /// Says the match kept here may be behind its journal, and has it read again by whoever takes the bots'
    /// turns: a step was refused or failed, and the line it was writing may be in the journal all the same.
    /// </summary>
    private void Behind(bool pump = true)
    {
        Volatile.Write(ref _stale, 1);
        if (pump)
        {
            Pump();
        }
    }

    /// <summary>
    /// The match where its journal ends, read now for a caller about to answer on it; none when it could not
    /// be read, which leaves it to be read again behind.
    /// </summary>
    private async Task<JournaledState?> CaughtUpAsync()
    {
        if (LetGo)
        {
            return null;
        }

        try
        {
            var read = await _stepper.ReadAsync(_match, _stopping);
            if (read.IsSuccess)
            {
                Take(read.Value);
                Pump();
                return read.Value;
            }
        }
        catch (Exception failure) when (Transient(failure))
        {
            // Said by whoever reads it again.
        }

        Behind();
        return null;
    }

    /// <summary>The match cannot be played on from its journal: the table ends without an outcome, and says why.</summary>
    private void Ended(DomainError error)
    {
        Console.WriteLine($"  Session {_match.Run} cannot be played on: {error.Message}");
        _outcome.TrySetResult(Result.Failure<MatchOutcome>(error));
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
