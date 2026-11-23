using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Table;
using DownfallArena.Infrastructure.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The notices held while the lobby's claim gate is (ADR 0203, #1133): given once the work has let go of it, whether it
/// finished or failed, and held again by work that starts after an earlier scope closed.
/// </summary>
public sealed class HeldNoticesTests
{
    private static readonly Notice Voided = new(NoticeKind.MatchOver, "Match voided", "Why.", "/lobby", "match:t1");

    private readonly AccountNoticeLog _log = new(new InMemoryNoticeStore(), TimeProvider.System);
    private readonly List<string> _said = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_notice_held_by_work_that_then_fails_is_still_given()
    {
        var notifier = new AccountNotifier(_log, push: null);
        var to = AccountId.New();

        await Should.ThrowAsync<IOException>(() => HeldNotices.AfterAsync<int>(
            async () =>
            {
                await HeldNotices.NotifyAsync(notifier, to, Voided);
                throw new IOException("The store is out.");
            },
            _said.Add));

        (await _log.ListAsync(to, Cancel)).ShouldHaveSingleItem().Title.ShouldBe("Match voided", "the players at the tables already let go of are told");
    }

    [Fact]
    public async Task Work_begun_inside_a_scope_that_has_closed_holds_its_notices_in_a_scope_of_its_own()
    {
        var notifier = new AccountNotifier(_log, push: null);
        var to = AccountId.New();
        var outerDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? later = null;
        var keptWhileWorking = -1;

        await HeldNotices.AfterAsync(
            () =>
            {
                // Background work inherits the scope it was started in: a loop a route starts runs on after the route answered.
                later = Task.Run(async () =>
                {
                    await outerDone.Task;
                    return await HeldNotices.AfterAsync(
                        async () =>
                        {
                            await HeldNotices.NotifyAsync(notifier, to, Voided);
                            keptWhileWorking = (await _log.ListAsync(to, Cancel)).Count;
                            return 0;
                        },
                        _said.Add);
                }, Cancel);
                return Task.FromResult(0);
            },
            _said.Add);
        outerDone.SetResult();
        await later!;

        keptWhileWorking.ShouldBe(0, "held until the work let go of the gate");
        (await _log.ListAsync(to, Cancel)).ShouldHaveSingleItem();
    }
}
