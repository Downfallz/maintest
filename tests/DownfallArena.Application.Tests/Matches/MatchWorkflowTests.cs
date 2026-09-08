using DownfallArena.Application.Matches;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using NSubstitute;

namespace DownfallArena.Application.Tests.Matches;

public sealed class MatchWorkflowTests
{
    [Fact]
    public async Task An_unknown_match_fails_without_touching_the_ports()
    {
        var store = new MatchStore();
        var unknown = MatchId.New();

        var command = await store.Workflow.ExecuteAsync(unknown, _ => Result.Success(), TestContext.Current.CancellationToken);
        var valued = await store.Workflow.ExecuteAsync(unknown, _ => Result.Success(1), TestContext.Current.CancellationToken);
        var query = await store.Workflow.QueryAsync(unknown, _ => 1, TestContext.Current.CancellationToken);

        command.Error.ShouldBe(ApplicationErrors.MatchNotFound);
        valued.Error.ShouldBe(ApplicationErrors.MatchNotFound);
        query.Error.ShouldBe(ApplicationErrors.MatchNotFound);
        await store.Repository.DidNotReceive().SaveAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
        await store.Dispatcher.DidNotReceive().DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_refused_action_saves_and_dispatches_nothing()
    {
        var store = new MatchStore();
        var match = store.Started();

        var result = await store.Workflow.ExecuteAsync(match.Id, current => current.SubmitSpeedChoice(PlayerSlot.Player1, new SpeedChoice(CreatureId.From(1), Speed.Quick)), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(RoundErrors.SpeedNotOpen);
        await store.Repository.DidNotReceive().SaveAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
        await store.Dispatcher.DidNotReceive().DispatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_accepted_action_saves_then_dispatches()
    {
        var store = new MatchStore();
        var match = store.Started();

        var result = await store.Workflow.ExecuteAsync(match.Id, current => current.PassEvolution(PlayerSlot.Player1), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        store.Calls.ShouldBe(["save", "dispatch"]);
        await store.Repository.Received(1).SaveAsync(match, Arg.Any<CancellationToken>());
        await store.Dispatcher.Received(1).DispatchAsync(match, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_query_projects_the_stored_match()
    {
        var store = new MatchStore();
        var match = store.Started();

        var result = await store.Workflow.QueryAsync(match.Id, current => current.State, TestContext.Current.CancellationToken);

        result.Value.ShouldBe(MatchState.InProgress);
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        var store = new MatchStore();

        await Should.ThrowAsync<ArgumentNullException>(() => store.Workflow.ExecuteAsync(MatchId.New(), (Func<Match, Result>)null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => store.Workflow.ExecuteAsync(MatchId.New(), (Func<Match, Result<int>>)null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => store.Workflow.QueryAsync(MatchId.New(), (Func<Match, int>)null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => store.Workflow.CommitAsync(null!, TestContext.Current.CancellationToken));
    }
}
