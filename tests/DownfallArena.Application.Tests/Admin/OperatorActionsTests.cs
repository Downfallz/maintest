using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;
using DownfallArena.Application.Tests.Support;

namespace DownfallArena.Application.Tests.Admin;

/// <summary>What operators did (admin plan, A6): noted with who and when, and read twenty a page, newest first.</summary>
public sealed class OperatorActionsTests
{
    private readonly Kept _log = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_action_is_noted_with_who_took_it_and_when()
    {
        var actions = new OperatorActions(_log, new FixedTimeProvider(FixedTimeProvider.Default));

        var noted = await actions.RecordAsync("mark", OperatorActions.Suspend, "acct_0000000000000001", "Spamming invitations.", Cancel);

        noted.ShouldSatisfyAllConditions(
            () => noted.At.ShouldBe(FixedTimeProvider.Default),
            () => noted.By.ShouldBe("mark"),
            () => noted.Action.ShouldBe(OperatorActions.Suspend),
            () => noted.Target.ShouldBe("acct_0000000000000001"),
            () => OperatorAction.IsId(noted.Id).ShouldBeTrue());
        _log.All.ShouldBe([noted]);
    }

    [Fact]
    public async Task Actions_are_read_twenty_a_page_newest_first_and_a_page_past_the_last_is_the_last()
    {
        for (var index = 0; index < 25; index++)
        {
            await new OperatorActions(_log, new FixedTimeProvider(FixedTimeProvider.Default.AddMinutes(index))).RecordAsync("mark", OperatorActions.OpenTable, $"t-{index}", null, Cancel);
        }

        var first = await new OperatorActions(_log, TimeProvider.System).PageAsync(1, Cancel);
        var past = await new OperatorActions(_log, TimeProvider.System).PageAsync(9, Cancel);

        first.Actions.Count.ShouldBe(OperatorActions.PerPage);
        first.Actions[0].Target.ShouldBe("t-24");
        (first.Total, first.Pages).ShouldBe((25, 2));
        past.Page.ShouldBe(2);
        past.Actions.Select(action => action.Target).ShouldBe(["t-4", "t-3", "t-2", "t-1", "t-0"]);
    }

    private sealed class Kept : IOperatorLog
    {
        private readonly List<OperatorAction> _kept = [];

        public IReadOnlyList<OperatorAction> All => _kept;

        public Task RecordAsync(OperatorAction action, CancellationToken cancellationToken = default)
        {
            _kept.Add(action);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> IdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([.. _kept.Select(action => action.Id).OrderDescending(StringComparer.Ordinal)]);

        public Task<OperatorAction?> FindAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_kept.FirstOrDefault(action => action.Id == id));
    }
}
