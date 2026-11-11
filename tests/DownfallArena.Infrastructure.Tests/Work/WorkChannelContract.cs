using DownfallArena.Application.Ports;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>
/// What every work queue does (ADR 0159, phase 5): an item received is hidden from every other receiver for as
/// long as it was received for, comes back delivered once more unless it is deleted, and comes back sooner when
/// it is retried.
/// </summary>
public abstract class WorkChannelContract
{
    private static readonly TimeSpan Briefly = TimeSpan.FromSeconds(1);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty queue.</summary>
    protected abstract Task<IWorkChannel> ChannelAsync();

    [Fact]
    public async Task An_item_is_received_once_while_it_is_hidden()
    {
        var channel = await ChannelAsync();
        await channel.SendAsync("""{"run":"table-1"}""", cancellationToken: Cancel);

        var first = await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);
        var second = await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);

        var received = first.ShouldHaveSingleItem();
        received.Body.ShouldBe("""{"run":"table-1"}""");
        received.Delivered.ShouldBe(1);
        second.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_item_not_deleted_comes_back_delivered_once_more()
    {
        var channel = await ChannelAsync();
        await channel.SendAsync("work", cancellationToken: Cancel);
        await channel.ReceiveAsync(10, Briefly, Cancel);

        await Task.Delay(Briefly * 2, Cancel);
        var again = await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);

        again.ShouldHaveSingleItem().Delivered.ShouldBe(2);
    }

    [Fact]
    public async Task A_deleted_item_never_comes_back()
    {
        var channel = await ChannelAsync();
        await channel.SendAsync("work", cancellationToken: Cancel);
        var received = (await channel.ReceiveAsync(10, Briefly, Cancel)).ShouldHaveSingleItem();

        await channel.DeleteAsync(received, Cancel);
        await Task.Delay(Briefly * 2, Cancel);

        (await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_retried_item_comes_back_when_it_is_told_to()
    {
        var channel = await ChannelAsync();
        await channel.SendAsync("work", cancellationToken: Cancel);
        var received = (await channel.ReceiveAsync(10, TimeSpan.FromMinutes(5), Cancel)).ShouldHaveSingleItem();

        await channel.RetryAsync(received, TimeSpan.Zero, Cancel);

        (await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel)).ShouldHaveSingleItem().Delivered.ShouldBe(2);
    }

    [Fact]
    public async Task An_item_sent_hidden_is_received_only_once_its_time_has_passed()
    {
        var channel = await ChannelAsync();

        await channel.SendAsync("later", hiddenFor: Briefly, cancellationToken: Cancel);
        var early = await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel);
        await Task.Delay(Briefly * 2, Cancel);

        early.ShouldBeEmpty();
        (await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel)).ShouldHaveSingleItem().Body.ShouldBe("later");
    }

    [Fact]
    public async Task An_item_kept_for_ever_is_received_like_any_other()
    {
        var channel = await ChannelAsync();

        await channel.SendAsync("dead letter", Timeout.InfiniteTimeSpan, cancellationToken: Cancel);

        (await channel.ReceiveAsync(10, TimeSpan.FromMinutes(1), Cancel)).ShouldHaveSingleItem().Body.ShouldBe("dead letter");
    }
}
