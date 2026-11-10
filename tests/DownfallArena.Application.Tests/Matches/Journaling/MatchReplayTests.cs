using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches.Journaling;

/// <summary>
/// A match is built again from how it was created and the decisions it took (ADR 0159), by the commands that
/// built it the first time.
/// </summary>
public sealed class MatchReplayTests
{
    private static readonly JournaledMatch Table = new("run-1", MatchId.New(), MatchStore.TwoOnTwo(), 3, MatchStore.Roster(MatchStore.TwoOnTwo()));

    [Fact]
    public async Task A_match_rebuilt_from_its_decisions_stands_where_they_left_it_with_the_answers_still_held()
    {
        var first = new Replica(new JournalStore());
        (await first.Replay.RebuildAsync(Table, [], TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var decisions = new List<SeatDecision>();
        var held = HeldAnswers.None;
        foreach (var decision in await OpeningAsync(first))
        {
            held = (await first.Stepper.SubmitAsync(Table.MatchId, MatchSeats.People, held, decision, TestContext.Current.CancellationToken)).Value.Held;
            decisions.Add(decision);
        }

        var second = new Replica(new JournalStore());

        var rebuilt = await second.Replay.RebuildAsync(Table, decisions, TestContext.Current.CancellationToken);

        rebuilt.IsSuccess.ShouldBeTrue();
        rebuilt.Value.ShouldBe(held);
        rebuilt.Value.Player1.ShouldHaveSingleItem().Kind.ShouldBe(PlayerOptionsKind.Speed);
        second.Events.Trace().ShouldBe(first.Events.Trace());
    }

    [Fact]
    public async Task A_line_the_rebuilt_match_does_not_take_is_a_divergence_that_names_the_line()
    {
        var replica = new Replica(new JournalStore());
        IReadOnlyList<SeatDecision> decisions =
        [
            new(PlayerSlot.Player1, PlayerDecision.Pass),
            new(PlayerSlot.Player1, PlayerDecision.Pass),
        ];

        var rebuilt = await replica.Replay.RebuildAsync(Table, decisions, TestContext.Current.CancellationToken);

        rebuilt.IsFailure.ShouldBeTrue();
        rebuilt.Error.Code.ShouldBe(JournalErrors.DivergedCode);
        rebuilt.Error.Message.ShouldContain("Line 2");
        rebuilt.Error.Message.ShouldContain(DecisionErrors.NotPending.Code);
    }

    [Fact]
    public async Task What_remembers_a_match_s_events_forgets_the_match_before_they_are_raised_again()
    {
        var memory = Substitute.For<IMatchMemory>();
        var replica = new Replica(new JournalStore(), memory);
        var savedWhenForgotten = new List<int>();
        memory.When(remembering => remembering.Forget(Table.MatchId)).Do(_ => savedWhenForgotten.Add(replica.Store.Calls.Count));

        await replica.Replay.RebuildAsync(Table, [], TestContext.Current.CancellationToken);
        var savedByOneRebuild = replica.Store.Calls.Count;
        await replica.Replay.RebuildAsync(Table, [], TestContext.Current.CancellationToken);

        savedByOneRebuild.ShouldBeGreaterThan(0);
        savedWhenForgotten.ShouldBe([0, savedByOneRebuild]);
    }

    /// <summary>Both seats pass their picks, and seat 1 chooses its first creature's speed: a line held, the rest revealed.</summary>
    private static async Task<IReadOnlyList<SeatDecision>> OpeningAsync(Replica replica)
    {
        var board = (await replica.Stepper.SeatAsync(Table.MatchId, PlayerSlot.Player1, HeldAnswers.None, TestContext.Current.CancellationToken)).Value.Board;
        return
        [
            new(PlayerSlot.Player1, PlayerDecision.Pass),
            new(PlayerSlot.Player2, PlayerDecision.Pass),
            new(PlayerSlot.Player1, PlayerDecision.ChooseSpeed(board.Allies[0].Id, Speed.Quick)),
        ];
    }
}
