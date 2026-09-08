using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Matches.Queries;

public sealed class QueryHandlerTests
{
    [Fact]
    public async Task The_board_state_query_projects_the_stored_match_for_the_slot()
    {
        var store = new MatchStore();
        var match = store.Started();
        var handler = new GetBoardStateForPlayerHandler(store.Workflow);

        var board = await handler.HandleAsync(new GetBoardStateForPlayer(match.Id, PlayerSlot.Player2));
        var missing = await handler.HandleAsync(new GetBoardStateForPlayer(MatchId.New(), PlayerSlot.Player2));

        board.Value.Slot.ShouldBe(PlayerSlot.Player2);
        board.Value.Allies.Select(creature => creature.Id).ShouldBe([CreatureId.From(3), CreatureId.From(4)]);
        missing.Error.ShouldBe(ApplicationErrors.MatchNotFound);
    }

    [Fact]
    public async Task The_options_query_projects_the_stored_match_for_the_slot()
    {
        var store = new MatchStore();
        var match = store.Started();
        var handler = new GetPlayerOptionsHandler(store.Workflow, TestContent.Resources);

        var options = await handler.HandleAsync(new GetPlayerOptions(match.Id, PlayerSlot.Player1));
        var missing = await handler.HandleAsync(new GetPlayerOptions(MatchId.New(), PlayerSlot.Player1));

        options.Value.Kind.ShouldBe(PlayerOptionsKind.Evolution);
        missing.Error.ShouldBe(ApplicationErrors.MatchNotFound);
    }

    [Fact]
    public async Task Null_queries_are_rejected()
    {
        var store = new MatchStore();

        await Should.ThrowAsync<ArgumentNullException>(() => new GetBoardStateForPlayerHandler(store.Workflow).HandleAsync(null!));
        await Should.ThrowAsync<ArgumentNullException>(() => new GetPlayerOptionsHandler(store.Workflow, TestContent.Resources).HandleAsync(null!));
    }
}
