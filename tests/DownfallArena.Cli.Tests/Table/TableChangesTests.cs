using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

public sealed class TableChangesTests
{
    [Fact]
    public async Task A_waiter_is_woken_by_the_next_change_and_told_the_version()
    {
        var changes = new TableChanges();
        var waiting = changes.WaitAsync(0, TestContext.Current.CancellationToken);
        waiting.IsCompleted.ShouldBeFalse();

        changes.Bump();

        (await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(1);
        changes.Version.ShouldBe(1);
    }

    [Fact]
    public async Task A_waiter_behind_the_version_is_answered_at_once_and_one_abreast_of_it_waits()
    {
        var changes = new TableChanges();
        changes.Bump();
        changes.Bump();

        (await changes.WaitAsync(0, TestContext.Current.CancellationToken)).ShouldBe(2);
        changes.WaitAsync(2, TestContext.Current.CancellationToken).IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Closing_wakes_every_waiter_with_the_version_as_it_stands_and_nothing_moves_after()
    {
        var changes = new TableChanges();
        changes.Bump();
        var one = changes.WaitAsync(1, TestContext.Current.CancellationToken);
        var other = changes.WaitAsync(1, TestContext.Current.CancellationToken);

        changes.Close();

        (await one.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(1);
        (await other.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBe(1);
        changes.IsClosed.ShouldBeTrue();
        changes.Bump();
        changes.Version.ShouldBe(1);
    }

    [Fact]
    public async Task A_wait_is_cancelled_like_any_other()
    {
        var changes = new TableChanges();
        using var cancelling = new CancellationTokenSource();
        var waiting = changes.WaitAsync(0, cancelling.Token);

        await cancelling.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
    }
}
