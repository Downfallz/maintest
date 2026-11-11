using DownfallArena.Application.Agents;
using DownfallArena.Application.Catalogue;
using DownfallArena.Application.Learning.Tracing;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A table's match stepped over its journal (ADR 0159): nothing waits on a person, a decision is written and
/// applied before it is answered, the bots' turns are taken behind it, and a table opened over a journal is
/// where the journal ends.
/// </summary>
public sealed class SteppedSessionTests : IDisposable
{
    private const string PersonName = "human:mk";

    private readonly HostedTables _host = new();
    private readonly HostedTables _other = new();
    private readonly Unreliable _journal = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<SteppedSession> _opened = [];
    private TableSeat[] _seats = [];

    public void Dispose()
    {
        _stopping.Cancel();
        foreach (var session in _opened)
        {
            session.Dispose();
        }

        _stopping.Dispose();
        _other.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task Nobody_is_asked_anything_until_the_table_begins_and_then_its_person_is_told_once()
    {
        var (session, person, _) = await Opened(_host);
        var told = new List<Asking>();
        person.Asked += (asking, _) => told.Add(asking);
        var before = person.Waiting;

        session.Begin();
        session.Begin();
        await session.SnapshotAsync(PlayerSlot.Player1);

        before.ShouldBeNull();
        session.HasBegun.ShouldBeTrue();
        person.Waiting.ShouldBe(new Asking(PlayerOptionsKind.Evolution, null, 1));
        told.ShouldBe([new Asking(PlayerOptionsKind.Evolution, null, 1)]);
    }

    /// <summary>
    /// The tap answers once its decision is written and applied. The bot in the other seat owes its speeds
    /// after it, and is still thinking when the tap has answered: its turn is taken behind, not inside.
    /// </summary>
    [Fact]
    public async Task A_decision_is_written_and_applied_before_it_answers_and_the_bot_s_turn_is_taken_behind_it()
    {
        var (session, person, bot) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        bot.Hold();

        var taken = await session.DecideAsync(PlayerSlot.Player1, PlayerDecision.Pass, 1).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var written = await Decisions();
        await bot.Asked.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var thinking = (await Decisions()).Count;
        var next = await Waiting(person, PlayerOptionsKind.Speed);
        bot.Release();
        await BotHeld(PlayerOptionsKind.Speed);

        taken.ShouldBe(new DecisionTaken(Taken: true, SetsOff: true));
        written.ShouldContain(new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass));
        thinking.ShouldBe(written.Count, "nothing of the bot's turn was written while it was thinking");
        next.Asked.ShouldBe(2, "the person is asked their own speeds while the bot thinks about its own");
    }

    [Fact]
    public async Task An_answer_to_an_asking_that_has_passed_and_one_for_a_seat_a_bot_holds_are_late()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        var before = (await Decisions()).Count;

        var stale = await session.DecideAsync(PlayerSlot.Player1, PlayerDecision.Pass, asked: 7);
        var bots = await session.DecideAsync(PlayerSlot.Player2, PlayerDecision.Pass, asked: 1);

        stale.ShouldBe(DecisionTaken.Late);
        bots.ShouldBe(DecisionTaken.Late);
        (await Decisions()).Count.ShouldBe(before);
    }

    /// <summary>
    /// A seat changes hands from the top of a round it has not reached. The round its person is being asked
    /// about is reached; the next is not, and the change waits there, written in the journal with what the seat
    /// had reached when it was asked.
    /// </summary>
    [Fact]
    public async Task A_seat_changes_hands_from_a_round_it_has_not_reached_and_the_journal_says_so()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        var greedy = Named(_host)(PlayerSlot.Player1, "greedy").ShouldNotBeNull();

        var reached = await session.SwapAsync(PlayerSlot.Player1, greedy, "greedy", round: 1);
        var later = await session.SwapAsync(PlayerSlot.Player1, greedy, "greedy", round: 2);

        reached.Taken.ShouldBeFalse();
        reached.Reached.ShouldBe(1);
        reached.Held.Name.ShouldBe(PersonName);
        later.Taken.ShouldBeTrue();
        session.Pending(PlayerSlot.Player1).ShouldBe(new Swap(greedy, 2));
        session.SeatedName(PlayerSlot.Player1).ShouldBe(PersonName);
        session.PlayedByItsPerson(PlayerSlot.Player1).ShouldBeTrue();
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Entries.OfType<SeatChangeEntry>()
            .ShouldBe([new SeatChangeEntry(PlayerSlot.Player1, "greedy", 2, Reached: 1)]);
    }

    /// <summary>
    /// Another process that holds nothing but the journal opens the table where it ends: the same question
    /// under the same asking, the same board, and whoever the journal's seat changes say sits where.
    /// </summary>
    [Fact]
    public async Task A_table_opened_over_its_journal_is_where_the_journal_ends_with_whoever_it_says_sits_there()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        (await session.DecideAsync(PlayerSlot.Player1, PlayerDecision.Pass, 1)).Taken.ShouldBeTrue();
        var asked = await Waiting(person, PlayerOptionsKind.Speed);
        await BotHeld(PlayerOptionsKind.Speed);
        (await session.SwapAsync(PlayerSlot.Player1, Named(_host)(PlayerSlot.Player1, "greedy")!, "greedy", round: 3)).Taken.ShouldBeTrue();
        var board = (await session.SnapshotAsync(PlayerSlot.Player1)).Value;

        var (elsewhere, there, _) = await Opened(_other, session.MatchId);
        var unbegun = there.Waiting;
        elsewhere.Begin();
        var read = (await elsewhere.SnapshotAsync(PlayerSlot.Player1)).Value;

        unbegun.ShouldBeNull();
        read.Waiting.ShouldBe(asked);
        System.Text.Json.JsonSerializer.Serialize(read.Board, ArtifactJson.LineOptions).ShouldBe(System.Text.Json.JsonSerializer.Serialize(board.Board, ArtifactJson.LineOptions));
        read.OpponentIntents.ShouldBe(board.OpponentIntents);
        elsewhere.SeatedName(PlayerSlot.Player1).ShouldBe(PersonName);
        elsewhere.Pending(PlayerSlot.Player1).ShouldNotBeNull().Round.ShouldBe(3);
        elsewhere.Pending(PlayerSlot.Player1)!.Next.Name.ShouldBe("greedy");
    }

    [Fact]
    public async Task A_table_of_bots_plays_itself_to_an_outcome_once_it_begins()
    {
        var (session, _, _) = await Opened(_host, people: false);
        var waited = session.Outcome.IsCompleted;

        session.Begin();
        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        waited.ShouldBeFalse();
        outcome.IsSuccess.ShouldBeTrue();
        session.IsOver.ShouldBeTrue();
        (await session.BoardAsync(PlayerSlot.Player1)).Value.Outcome.ShouldBe(outcome.Value);
    }

    [Fact]
    public async Task A_concession_ends_the_table_at_once_and_is_the_journal_s_last_line()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);

        var conceded = await session.ConcedeAsync(PlayerSlot.Player1);

        conceded.IsSuccess.ShouldBeTrue();
        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        outcome.Value.Winner.ShouldBe(PlayerSlot.Player2);
        person.Waiting.ShouldBeNull();
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Entries[^1].ShouldBe(new ConcessionEntry(PlayerSlot.Player1));
    }

    /// <summary>
    /// Only the seat's own person gives its match up, and the session asks that itself, where the match ends:
    /// the route's own look at the seat comes before the concession has waited for the match, behind a bot's
    /// turn that can carry the seat past a handover. Here a bot holds the person's seat until round 2, and the
    /// concession waits behind the other bot's first turn of round 1.
    /// </summary>
    [Fact]
    public async Task A_concession_for_a_seat_a_bot_holds_when_it_reaches_the_match_is_refused_and_ends_nothing()
    {
        var (session, _, bot) = await Opened(_host, handover: 2);
        bot.Hold();
        session.Begin();
        await bot.Asked.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var conceding = session.ConcedeAsync(PlayerSlot.Player1);
        bot.Release();
        var conceded = await conceding;

        conceded.IsFailure.ShouldBeTrue();
        conceded.Error.ShouldBe(SeatErrors.PlayedByABot);
        (await session.BoardAsync(PlayerSlot.Player1)).Value.Outcome.ShouldBeNull();
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Entries.OfType<ConcessionEntry>().ShouldBeEmpty();
    }

    /// <summary>
    /// A seat is shown the feed of the match its journal holds, and no further: the entries of a bot's turn that
    /// was applied and could not be written are of a match that is not the table's, and are recorded again
    /// under the same numbers once the turn is taken again.
    /// </summary>
    [Fact]
    public async Task A_seat_is_not_shown_the_entries_of_a_turn_its_journal_does_not_hold()
    {
        var (session, _, _) = await Opened(_host, people: false);
        var api = Api(session);
        var journaled = session.Journaled;
        _journal.FailTheNext(3);

        session.Begin();
        await Failed();
        var shown = await FeedNextAsync(api);

        shown.ShouldBe(journaled.ShouldNotBeNull());
        _host.Services.GetRequiredService<MatchTraceRecorder>().Length(session.MatchId).ShouldBeGreaterThan(shown, "the turn that was not written has recorded its entries");
    }

    /// <summary>
    /// A table's pages are told what changed once the match that changed is the journal's: a bot's turn that
    /// records its entries and is not written moves nothing, where every entry recorded used to.
    /// </summary>
    [Fact]
    public async Task A_turn_its_journal_does_not_hold_does_not_move_the_table_s_version()
    {
        var (session, _, _) = await Opened(_host, people: false);
        var api = Api(session);
        var before = api.Changes.Version;
        _journal.FailTheNext(3);

        session.Begin();
        await Failed();

        api.Changes.Version.ShouldBe(before);
    }

    /// <summary>
    /// A table let go of has no outcome, keeps nothing in the host's stores, and takes nothing more: a tap, a
    /// concession or a seat change that arrives after must not bring back the match that was just forgotten.
    /// </summary>
    [Fact]
    public async Task A_table_let_go_of_has_no_outcome_forgets_its_match_and_takes_nothing_more()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        var written = (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Position;

        await _stopping.CancelAsync();
        await Task.WhenAny(session.Outcome);
        var tap = await session.DecideAsync(PlayerSlot.Player1, PlayerDecision.Pass, 1);
        var conceded = await session.ConcedeAsync(PlayerSlot.Player1);
        var swapped = await session.SwapAsync(PlayerSlot.Player1, Named(_host)(PlayerSlot.Player1, "greedy")!, "greedy", round: 3);

        session.IsOver.ShouldBeTrue();
        session.Outcome.IsCanceled.ShouldBeTrue();
        tap.ShouldBe(DecisionTaken.Late);
        conceded.IsFailure.ShouldBeTrue();
        swapped.Taken.ShouldBeFalse();
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Position.ShouldBe(written);
        (await _host.Services.GetRequiredService<IMatchRepository>().FindAsync(session.MatchId, TestContext.Current.CancellationToken)).ShouldBeNull();
        _host.Services.GetRequiredService<MatchTraceRecorder>().Length(session.MatchId).ShouldBe(0);
    }

    /// <summary>
    /// A line can be in the journal with its answer lost on the way back: the writer is told the match moved.
    /// The tap is answered as late, and the seat reads on from the journal rather than staying on a question
    /// the journal says it has answered.
    /// </summary>
    [Fact]
    public async Task A_decision_written_whose_answer_was_lost_is_late_and_the_seat_reads_on_from_the_journal()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        _journal.LoseTheNextAnswer();

        var taken = await session.DecideAsync(PlayerSlot.Player1, PlayerDecision.Pass, 1);
        var next = await Waiting(person, PlayerOptionsKind.Speed);

        taken.ShouldBe(DecisionTaken.Late);
        next.Asked.ShouldBe(2);
        (await Decisions()).Count(decision => decision == new SeatDecision(PlayerSlot.Player1, PlayerDecision.Pass)).ShouldBe(1);
    }

    [Fact]
    public async Task A_concession_written_whose_answer_was_lost_is_the_match_conceded()
    {
        var (session, person, _) = await Opened(_host);
        session.Begin();
        await Waiting(person, PlayerOptionsKind.Evolution);
        await BotHeld(PlayerOptionsKind.Evolution);
        _journal.LoseTheNextAnswer();

        var conceded = await session.ConcedeAsync(PlayerSlot.Player1);

        conceded.IsSuccess.ShouldBeTrue();
        (await session.Outcome.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Value.Winner.ShouldBe(PlayerSlot.Player2);
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Entries.OfType<ConcessionEntry>().ShouldHaveSingleItem();
    }

    /// <summary>
    /// Nobody may be reading a table when a store is out under a bot's turn -- a table of bots, a page closed
    /// while the bot thinks -- so the turn is taken again by the table itself, after a wait.
    /// </summary>
    [Fact]
    public async Task A_bot_s_turn_that_could_not_be_written_is_taken_again_without_anybody_reading_the_table()
    {
        var (session, _, _) = await Opened(_host, people: false);
        _journal.FailTheNext(2);

        session.Begin();
        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue();
        _journal.Failed.ShouldBe(2);
    }

    private const string Run = "table-1";

    /// <summary>The routes over a session opened here, under the seats it was opened with.</summary>
    private TableApi Api(SteppedSession session) =>
        new(session, _seats, CatalogueProjection.Build(_host.Services.GetRequiredService<IGameResources>(), HostedTables.Rules), _host.Services.GetRequiredService<MatchTraceRecorder>());

    /// <summary>Where a seat's next read of the feed starts, as the seat route says: how much of it the seat was shown.</summary>
    private static async Task<int> FeedNextAsync(TableApi api)
    {
        var answer = await api.HandleAsync("GET", "/api/seat/player1", string.Empty, "seat-1");
        answer.Status.ShouldBe(200);
        using var payload = System.Text.Json.JsonDocument.Parse(answer.Body);
        return payload.RootElement.GetProperty("feedNext").GetInt32();
    }

    /// <summary>Waits until the journal has refused an append.</summary>
    private async Task Failed()
    {
        for (var attempt = 0; attempt < 500 && _journal.Failed == 0; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        _journal.Failed.ShouldBeGreaterThan(0);
    }

    /// <summary>A table with its person in seat 1 and a bot that can be held in seat 2, or two bots.</summary>
    /// <param name="handover">The round the person's seat is theirs from, a bot holding it until then; none for a seat theirs from the start.</param>
    private async Task<(SteppedSession Session, SteppedPerson Person, Held Bot)> Opened(HostedTables host, MatchId? matchId = null, bool people = true, int? handover = null)
    {
        var resources = host.Services.GetRequiredService<IGameResources>();
        var person = new SteppedPerson();
        var bot = new Held(new GreedyAgent(resources, HostedTables.Rules));
        TableSeat[] seats = [new TableSeat(PlayerSlot.Player1, "seat-1", people ? person : null), new TableSeat(PlayerSlot.Player2, "seat-2", Person: null)];
        _seats = seats;
        var plan = new SeatPlan(
            people ? SeatPlan.Person(PersonName) : new Occupant(new GreedyAgent(resources, HostedTables.Rules), "greedy"),
            new Occupant(bot, "greedy"));
        if (handover is { } round)
        {
            plan = new SeatPlan(new Occupant(new GreedyAgent(resources, HostedTables.Rules), "greedy"), new Occupant(bot, "greedy"))
                .From(PlayerSlot.Player1, round, SeatPlan.Person(PersonName), reached: 0);
        }

        List<CreatureDefinitionId> roster = [.. Enumerable.Repeat(resources.Creatures.First().Id, HostedTables.Rules.TeamSize)];
        var opened = await SteppedSession.OpenAsync(
            SteppedHost.Of(host.Services),
            _journal,
            new JournaledMatch(Run, matchId ?? MatchId.New(), HostedTables.Rules, 7, roster),
            new SteppedSeats(plan, seats, Named(host)),
            stopping: _stopping.Token);
        opened.IsSuccess.ShouldBeTrue(opened.IsFailure ? opened.Error.Message : string.Empty);
        _opened.Add(opened.Value);
        return (opened.Value, person, bot);
    }

    /// <summary>Who a name stands for, as a pilot's seating says: the seat's own person, or the bot of that name.</summary>
    private static Func<PlayerSlot, string, Occupant?> Named(HostedTables host) => (_, wanted) =>
        wanted == TableRequest.Person
            ? SeatPlan.Person(PersonName)
            : new Occupant(new GreedyAgent(host.Services.GetRequiredService<IGameResources>(), HostedTables.Rules), wanted);

    private async Task<IReadOnlyList<SeatDecision>> Decisions() =>
        (await _journal.ReadAsync(Run, TestContext.Current.CancellationToken)).Decisions;

    /// <summary>Waits until the bot's answers to what both seats are asked at once are in the journal.</summary>
    private async Task BotHeld(PlayerOptionsKind kind)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if ((await Decisions()).Any(decision => decision.Slot == PlayerSlot.Player2 && decision.Decision.Kind == kind))
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new InvalidOperationException($"The bot never answered its {kind}.");
    }

    private static async Task<Asking> Waiting(SteppedPerson person, PlayerOptionsKind kind)
    {
        for (var attempt = 0; attempt < 500 && person.Waiting?.Kind != kind; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var waiting = person.Waiting.ShouldNotBeNull();
        waiting.Kind.ShouldBe(kind);
        return waiting;
    }

    /// <summary>A journal whose store can be made to fail: an append that throws, or one written whose answer is lost.</summary>
    private sealed class Unreliable : IDecisionJournal
    {
        private readonly InMemoryDecisionJournal _inner = new();
        private int _failing;
        private int _losing;

        private int _failed;

        /// <summary>How many appends were refused by the store.</summary>
        public int Failed => Volatile.Read(ref _failed);

        public void FailTheNext(int appends) => Volatile.Write(ref _failing, appends);

        public void LoseTheNextAnswer() => Volatile.Write(ref _losing, 1);

        public Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default) => _inner.ReadAsync(run, cancellationToken);

        public Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default) => _inner.ReadAfterAsync(run, after, cancellationToken);

        public Task<long> EndAsync(string run, CancellationToken cancellationToken = default) => _inner.EndAsync(run, cancellationToken);

        public async Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count > 0 && Volatile.Read(ref _failing) > 0)
            {
                Interlocked.Decrement(ref _failing);
                Interlocked.Increment(ref _failed);
                throw new IOException("The store is out.");
            }

            var written = await _inner.AppendAsync(run, after, entries, cancellationToken);
            return entries.Count > 0 && Interlocked.Exchange(ref _losing, 0) != 0 ? null : written;
        }
    }

    /// <summary>A bot that can be made to stop and think at its next decision, and says when it has.</summary>
    private sealed class Held(IPlayerAgent inner) : IPlayerAgent
    {
        private readonly TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile TaskCompletionSource? _thinking;

        /// <summary>Completes when the bot is asked something while it is held.</summary>
        public Task Asked => _asked.Task;

        public void Hold() => _thinking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _thinking?.TrySetResult();

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Thought().DecideEvolution(board, options);

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Thought().DecideSpeed(board, creature);

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => Thought().DecideTieOrder(board, options);

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Thought().DecideIntent(board, intentOption);

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Thought().DecideTargets(board, options);

        public ChosenAction DecideAction(PlayerBoardState board, ActionOptions options) => Thought().DecideAction(board, options);

        private IPlayerAgent Thought()
        {
            if (_thinking is { } thinking)
            {
                _asked.TrySetResult();
                thinking.Task.Wait(TimeSpan.FromSeconds(30));
            }

            return inner;
        }
    }
}
