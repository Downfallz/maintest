using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Cli.Table;
using DownfallArena.Cli.Tests.Studio;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources.Authoring;
using DownfallArena.SharedKernel.Identifiers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// A session is a real match through the engine, with no HTTP anywhere near it. These play one with scripted
/// seats, which is what the host will do with people in them.
/// </summary>
public sealed class TableSessionTests : IDisposable
{
    private readonly StudioContent _content = new();
    private IHost? _host;

    public void Dispose()
    {
        _host?.Dispose();
        _content.Dispose();
    }

    [Fact]
    public async Task Two_seats_play_a_match_through_the_engine_to_its_outcome()
    {
        var services = Built();
        using var session = await TableSession.StartAsync(services, RuleSet.Create(2, 2, 1, 6, 2.0), seed: 7, new SeatAgent(new Occupant(Bot(services), "greedy")), new SeatAgent(new Occupant(Bot(services), "greedy")), cancellationToken: TestContext.Current.CancellationToken);

        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue(outcome.IsFailure ? outcome.Error.Message : null);
        session.IsOver.ShouldBeTrue();
        outcome.Value.Reason.ShouldBeOneOf(MatchEndReason.Elimination, MatchEndReason.RoundCap);
    }

    [Fact]
    public void A_seat_forwards_every_decision_to_whoever_is_seated()
    {
        var first = new Occupant(new Counting(new Refusing()), "first");
        var second = new Occupant(new Counting(new Refusing()), "second");
        var seat = new SeatAgent(first);

        seat.Seated.ShouldBeSameAs(first);
        seat.Seat(second).ShouldBeSameAs(first);
        seat.Seated.ShouldBeSameAs(second);
    }

    /// <summary>The least board a seat needs to answer: the round it is in.</summary>
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

    /// <summary>
    /// A seat refuses to be handed over for a round it is already playing, and the refusal says which.
    /// </summary>
    /// <remarks>
    /// The check belongs here rather than in whoever asks, because a caller that read the board and then
    /// installed a swap does those in two steps against a match that is moving: the driver can enter the very
    /// round the request names in between, and the swap then lands inside it. The seat sees every round as it
    /// is asked about it, so under its own lock the newest round it has seen is the newest round there is.
    /// </remarks>
    [Fact]
    public void A_seat_refuses_to_change_hands_for_a_round_it_is_already_playing()
    {
        var seat = new SeatAgent(new Occupant(new Counting(new Refusing()), "first"));
        var next = new Occupant(new Counting(new Refusing()), "second");

        // Nothing has been asked yet, so no round has begun for this seat and any round will do.
        seat.SwapAt(next, round: 1).ShouldBeNull();

        seat.Deciding(BoardIn(round: 4));

        seat.SwapAt(next, round: 4)!.Reached.ShouldBe(4);
        seat.SwapAt(next, round: 2)!.Reached.ShouldBe(4);
        seat.SwapAt(next, round: 5).ShouldBeNull();
    }

    /// <summary>A seat that has seen a round does not forget it because a later board reads lower.</summary>
    [Fact]
    public void A_seat_refuses_against_the_newest_round_it_has_seen()
    {
        var seat = new SeatAgent(new Occupant(new Counting(new Refusing()), "first"));

        seat.Deciding(BoardIn(round: 6));
        seat.Deciding(BoardIn(round: 3));

        seat.SwapAt(new Occupant(new Counting(new Refusing()), "second"), round: 5)!.Reached.ShouldBe(6);
    }

    /// <summary>
    /// The swap is the mechanism behind every piloting capability, so what matters is that the match does not
    /// notice: it keeps asking the seat, and the seat keeps answering, with someone else behind it.
    /// </summary>
    [Fact]
    public async Task A_seat_handed_to_someone_else_mid_match_keeps_the_match_running()
    {
        var services = Built();
        var first = new Counting(Bot(services));
        var second = new Counting(Bot(services));
        using var session = await TableSession.StartAsync(services, RuleSet.Create(2, 2, 1, 6, 2.0), seed: 7, new SeatAgent(new Occupant(first, "first")), new SeatAgent(new Occupant(Bot(services), "greedy")), cancellationToken: TestContext.Current.CancellationToken);

        first.Asked.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue("the first seat should have been asked something");
        session.Player1.Seat(new Occupant(second, "second"));
        var outcome = await session.Outcome.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        outcome.IsSuccess.ShouldBeTrue(outcome.IsFailure ? outcome.Error.Message : null);
        first.Decisions.ShouldBeGreaterThan(0, "the first agent decided before the swap");
        second.Decisions.ShouldBeGreaterThan(0, "the second agent decided after it");
    }

    private IServiceProvider Built()
    {
        new ContentStore(_content.Path).Build(Path.Combine(_content.Path, "dst"));
        _host = CliHost.Build(
            new CliOptions { Command = "table", Output = "out.csv", SchemaPath = Path.Combine(_content.Path, "dst", "game.schema.json") },
            seed: 7,
            logMatchToConsole: false);
        return _host.Services;
    }

    private static GreedyAgent Bot(IServiceProvider services) =>
        new GreedyAgent(services.GetRequiredService<IGameResources>(), RuleSet.Create(2, 2, 1, 6, 2.0));

    /// <summary>Counts what it was asked and says when it was first asked, so a test can swap deterministically.</summary>
    private sealed class Counting(IPlayerAgent inner) : IPlayerAgent
    {
        private int _decisions;

        public ManualResetEvent Asked { get; } = new(false);

        public int Decisions => Volatile.Read(ref _decisions);

        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => Count(() => inner.DecideEvolution(board, options));

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => Count(() => inner.DecideSpeed(board, creature));

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => Count(() => inner.DecideIntent(board, intentOption));

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => Count(() => inner.DecideTargets(board, options));

        private TDecision Count<TDecision>(Func<TDecision> decide)
        {
            Interlocked.Increment(ref _decisions);
            Asked.Set();
            return decide();
        }
    }

    /// <summary>A seat nobody sits in. Every call is a bug in the seat, so every call throws.</summary>
    private sealed class Refusing : IPlayerAgent
    {
        public EvolutionDecision DecideEvolution(PlayerBoardState board, EvolutionOptions options) => throw new InvalidOperationException("asked");

        public Speed DecideSpeed(PlayerBoardState board, CreatureId creature) => throw new InvalidOperationException("asked");

        public SpellId DecideIntent(PlayerBoardState board, IntentOption intentOption) => throw new InvalidOperationException("asked");

        public IReadOnlyList<CreatureId> DecideTargets(PlayerBoardState board, TargetOptions options) => throw new InvalidOperationException("asked");
    }
}
