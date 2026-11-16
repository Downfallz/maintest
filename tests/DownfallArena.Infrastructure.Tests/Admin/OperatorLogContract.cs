using DownfallArena.Application.Admin;
using DownfallArena.Application.Admin.Ports;

namespace DownfallArena.Infrastructure.Tests.Admin;

/// <summary>What every operator log promises (admin plan, A6), played against each adapter.</summary>
public abstract class OperatorLogContract
{
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 3, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IOperatorLog> LogAsync();

    private static OperatorAction Action(DateTimeOffset at) =>
        new(OperatorAction.NewId(at), at, "mark", OperatorActions.Suspend, "acct_0000000000000001", "Spamming invitations to strangers.");

    [Fact]
    public async Task An_action_reads_back_as_it_was_recorded()
    {
        var log = await LogAsync();
        var action = Action(Start);

        await log.RecordAsync(action, Cancel);

        (await log.FindAsync(action.Id, Cancel)).ShouldBe(action);
    }

    [Fact]
    public async Task Ids_are_listed_newest_first()
    {
        var log = await LogAsync();
        OperatorAction[] actions = [Action(Start.AddMinutes(1)), Action(Start), Action(Start.AddMinutes(2))];
        foreach (var action in actions)
        {
            await log.RecordAsync(action, Cancel);
        }

        (await log.IdsAsync(Cancel)).ShouldBe([actions[2].Id, actions[0].Id, actions[1].Id]);
    }

    [Fact]
    public async Task A_log_with_nothing_kept_lists_nothing_and_finds_nothing_under_an_id_it_never_made()
    {
        var log = await LogAsync();

        (await log.IdsAsync(Cancel)).ShouldBeEmpty();
        (await log.FindAsync(OperatorAction.NewId(Start), Cancel)).ShouldBeNull();
        (await log.FindAsync("../escape", Cancel)).ShouldBeNull();
    }
}
