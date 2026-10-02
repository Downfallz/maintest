using System.Text.Json;
using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Cli.Table;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The seed and the decisions are the match (ADR 0091): a journal written by one table, replayed into a fresh
/// match of the same seed, rebuilds the same board without asking anybody anything.
/// </summary>
public sealed class DecisionJournalTests : IDisposable
{
    private readonly HostedTables _hosted = new(recording: true);

    public void Dispose() => _hosted.Dispose();

    [Fact]
    public async Task A_journal_replayed_into_a_fresh_match_of_the_same_seed_rebuilds_the_same_match()
    {
        var played = await _hosted.Composer.ComposeAsync(HostedTables.Bots, TestContext.Current.CancellationToken);
        var outcome = await played.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        outcome.IsSuccess.ShouldBeTrue();
        await Finished(played);
        var recorded = await _hosted.Stored!.JournalAsync(played.Id, TestContext.Current.CancellationToken);
        recorded.Count.ShouldBeGreaterThan(10);
        recorded.Select(entry => entry.Seq).ShouldBe(Enumerable.Range(1, recorded.Count).Select(seq => (long)seq));

        var journal = new DecisionJournal(new FileArtifactWriter(Path.Combine(_hosted.RunsDirectory, "replay")), TimeProvider.System, recorded);
        var seat1 = new SeatAgent(new Occupant(new Never(), "nobody"));
        var seat2 = new SeatAgent(new Occupant(new Never(), "nobody"));
        using var replayed = await TableSession.StartAsync(
            _hosted.Services,
            HostedTables.Rules,
            HostedTables.Bots.Seed!.Value,
            seat1,
            seat2,
            (_, seat) => journal.Around(seat, ReferenceEquals(seat, seat1) ? PlayerSlot.Player1 : PlayerSlot.Player2),
            cancellationToken: TestContext.Current.CancellationToken);

        var again = await replayed.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        again.IsSuccess.ShouldBeTrue(again.IsFailure ? again.Error.Message : null);
        again.Value.ShouldBe(outcome.Value);
        journal.Exhausted.IsCompletedSuccessfully.ShouldBeTrue();
        journal.Replaying.ShouldBeFalse();
        (await Board(replayed, PlayerSlot.Player1)).ShouldBe(await Board(played.Session, PlayerSlot.Player1));
        (await Board(replayed, PlayerSlot.Player2)).ShouldBe(await Board(played.Session, PlayerSlot.Player2));
    }

    [Fact]
    public async Task A_line_that_does_not_answer_the_question_asked_is_a_divergence_and_the_replay_is_refused()
    {
        var played = await _hosted.Composer.ComposeAsync(HostedTables.Bots, TestContext.Current.CancellationToken);
        await played.Session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await Finished(played);
        var recorded = (await _hosted.Stored!.JournalAsync(played.Id, TestContext.Current.CancellationToken)).ToList();
        recorded[3] = recorded[3] with { Slot = recorded[3].Slot == "player1" ? "player2" : "player1" };

        var journal = new DecisionJournal(new FileArtifactWriter(Path.Combine(_hosted.RunsDirectory, "diverged")), TimeProvider.System, recorded);
        var seat1 = new SeatAgent(new Occupant(new Never(), "nobody"));
        var seat2 = new SeatAgent(new Occupant(new Never(), "nobody"));
        using var replayed = await TableSession.StartAsync(
            _hosted.Services,
            HostedTables.Rules,
            HostedTables.Bots.Seed!.Value,
            seat1,
            seat2,
            (_, seat) => journal.Around(seat, ReferenceEquals(seat, seat1) ? PlayerSlot.Player1 : PlayerSlot.Player2),
            cancellationToken: TestContext.Current.CancellationToken);

        await Task.WhenAny(replayed.Outcome).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        replayed.Outcome.IsFaulted.ShouldBeTrue("a record that does not fit the match is refused, not guessed at");
        journal.Exhausted.IsFaulted.ShouldBeTrue();
        journal.Exhausted.Exception!.GetBaseException().Message.ShouldContain("diverged");
    }

    [Fact]
    public async Task A_swap_the_pilot_asked_for_is_written_where_it_was_asked_and_applied_there_on_replay()
    {
        var directory = Path.Combine(_hosted.RunsDirectory, "swaps");
        var writer = new FileArtifactWriter(directory);
        var written = new DecisionJournal(writer, TimeProvider.System);
        var scripted = written.Around(new Passing(), PlayerSlot.Player1);
        scripted.DecideEvolution(BoardIn(1), new EvolutionOptions(0, []));
        written.SwapAsked(PlayerSlot.Player1, "greedy", 3);
        scripted.DecideSpeed(BoardIn(1), CreatureId.From(1));
        await written.Written;

        var lines = (await File.ReadAllTextAsync(Path.Combine(directory, DecisionJournal.File), TestContext.Current.CancellationToken)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var recorded = lines.Select(line => JsonSerializer.Deserialize<JournalEntry>(line, ArtifactJson.LineOptions)!).ToList();
        recorded.Select(entry => entry.Kind).ShouldBe(["Evolution", "Swap", "Speed"]);
        recorded[1].To.ShouldBe("greedy");
        recorded[1].AtRound.ShouldBe(3);

        var applied = new List<(PlayerSlot Slot, string To, int Round)>();
        var replaying = new DecisionJournal(new FileArtifactWriter(Path.Combine(directory, "again")), TimeProvider.System, recorded);
        replaying.SwapsThrough((slot, to, round) =>
        {
            applied.Add((slot, to, round));
            return true;
        });
        var replayed = replaying.Around(new Never(), PlayerSlot.Player1);

        replayed.DecideEvolution(BoardIn(1), new EvolutionOptions(0, [])).Choice.ShouldBeNull("the recorded pass");

        // Applied with the decision it followed: the seat had reached the same round either way, so the swap
        // lands on the same question it would have.
        applied.ShouldBe([(PlayerSlot.Player1, "greedy", 3)]);
        replaying.Exhausted.IsCompleted.ShouldBeFalse("a speed is still to replay");
        replayed.DecideSpeed(BoardIn(1), CreatureId.From(1)).ShouldBe(Speed.Quick);
        applied.Count.ShouldBe(1);
        replaying.Exhausted.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>A recorded swap that cannot be applied any more is a divergence: the record says a seat changed hands.</summary>
    [Fact]
    public void A_swap_that_can_no_longer_be_applied_is_a_divergence()
    {
        var recorded = new List<JournalEntry>
        {
            JournalEntry.Swap(1, DateTimeOffset.UtcNow, PlayerSlot.Player1, "heuristic:gone.json", 3),
            JournalEntry.Of(2, DateTimeOffset.UtcNow, PlayerSlot.Player1, PlayerDecision.Pass),
        };
        var replaying = new DecisionJournal(new FileArtifactWriter(Path.Combine(_hosted.RunsDirectory, "refused")), TimeProvider.System, recorded);
        replaying.SwapsThrough((_, _, _) => false);
        var replayed = replaying.Around(new Never(), PlayerSlot.Player1);

        Should.Throw<InvalidOperationException>(() => replayed.DecideEvolution(BoardIn(1), new EvolutionOptions(0, [])));

        replaying.Exhausted.IsFaulted.ShouldBeTrue();
    }

    private static async Task<string> Board(TableSession session, PlayerSlot slot)
    {
        var board = await session.Queries.GetBoardStateForPlayer.HandleAsync(new GetBoardStateForPlayer(session.MatchId, slot), TestContext.Current.CancellationToken);
        board.IsSuccess.ShouldBeTrue();

        // The replayed session is another match of the same seed, so its id is its own; everything else is held.
        return JsonSerializer.Serialize(board.Value with { MatchId = MatchId.From(Guid.Parse("00000000-0000-0000-0000-000000000001")) }, ArtifactJson.LineOptions);
    }

    private static PlayerBoardState BoardIn(int round) => new()
    {
        MatchId = MatchId.New(),
        Slot = PlayerSlot.Player1,
        State = MatchState.InProgress,
        ContentHash = "hash",
        RoundNumber = round,
        Allies = [],
        Enemies = [],
    };

    private static async Task Finished(PlayedTable table)
    {
        for (var attempt = 0; attempt < 300 && !table.IsFinished; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        table.IsFinished.ShouldBeTrue();
    }

    /// <summary>A seat the replay must never reach: every line of the record answers before it is asked.</summary>
    private sealed class Never : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => throw new InvalidOperationException("The replay asked a seat.");

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => throw new InvalidOperationException("The replay asked a seat.");

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => throw new InvalidOperationException("The replay asked a seat.");

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => throw new InvalidOperationException("The replay asked a seat.");

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => throw new InvalidOperationException("The replay asked a seat.");
    }

    /// <summary>A seat that passes and goes Quick, enough to write two lines.</summary>
    private sealed class Passing : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => EvolutionDecision.Pass;

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Speed.Quick;

        public IReadOnlyList<CreatureId> DecideTieOrder(PlayerBoardState board, TieOrderOptions options) => [];

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => intentOption.CastableSpells[0];

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => [];
    }
}

