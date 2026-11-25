using DownfallArena.Infrastructure.Accounts;
using Microsoft.Extensions.Time.Testing;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What a store keeps of a read, so a write over that version reads the document no second time (#1232).</summary>
public sealed class TextsReadTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private int _reads;

    private Func<CancellationToken, Task<(string? Text, string? Version)>> Stored(string? text, string? version) => _ =>
    {
        _reads++;
        return Task.FromResult((text, version));
    };

    [Fact]
    public async Task A_write_over_the_version_kept_takes_its_text_and_reads_nothing()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v1");

        (await texts.AtAsync("a", "v1", Stored("stored", "v1"), Cancel)).ShouldBe((true, "kept"));

        _reads.ShouldBe(0);
    }

    [Fact]
    public async Task What_was_kept_goes_to_one_write_and_the_next_reads_again()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v1");
        await texts.AtAsync("a", "v1", Stored("stored", "v1"), Cancel);

        (await texts.AtAsync("a", "v1", Stored("stored", "v1"), Cancel)).ShouldBe((true, "stored"));

        _reads.ShouldBe(1);
    }

    [Fact]
    public async Task A_write_over_another_version_reads_again_and_is_refused_when_the_document_moved_past_it()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v2");

        (await texts.AtAsync("a", "v1", Stored("stored", "v2"), Cancel)).ShouldBe((false, null));

        _reads.ShouldBe(1);
    }

    [Fact]
    public async Task A_document_read_as_absent_forgets_what_was_kept_of_it()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v1");
        texts.Remember("a", null, null);

        (await texts.AtAsync("a", "v1", Stored(null, null), Cancel)).ShouldBe((false, null));

        _reads.ShouldBe(1);
    }

    [Fact]
    public async Task A_document_forgotten_is_read_again()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v1");
        texts.Remember("b", "kept", "v1");
        texts.Remember("c", "kept", "v1");

        texts.Forget("a");
        texts.Forget(key => key == "b");

        await texts.AtAsync("a", "v1", Stored("stored", "v1"), Cancel);
        await texts.AtAsync("b", "v1", Stored("stored", "v1"), Cancel);
        await texts.AtAsync("c", "v1", Stored("stored", "v1"), Cancel);
        _reads.ShouldBe(2);
    }

    [Fact]
    public async Task Past_its_capacity_a_new_document_forgets_the_rest_and_one_read_again_does_not()
    {
        var texts = new TextsRead<int>();
        foreach (var key in Enumerable.Range(0, 256))
        {
            texts.Remember(key, "kept", "v1");
        }

        texts.Remember(0, "kept", "v2");
        (await texts.AtAsync(1, "v1", Stored("stored", "v1"), Cancel)).ShouldBe((true, "kept"));
        _reads.ShouldBe(0);
        texts.Remember(1, "kept", "v1");

        texts.Remember(256, "kept", "v1");
        await texts.AtAsync(2, "v1", Stored("stored", "v1"), Cancel);
        (await texts.AtAsync(256, "v1", Stored("stored", "v1"), Cancel)).ShouldBe((true, "kept"));
        _reads.ShouldBe(1);
    }

    [Fact]
    public async Task A_text_kept_past_its_lifetime_is_read_again()
    {
        var time = new FakeTimeProvider();
        var texts = new TextsRead<string>(time);
        texts.Remember("a", "kept", "v1");

        time.Advance(TimeSpan.FromSeconds(TextsRead<string>.LifetimeSeconds));

        (await texts.AtAsync("a", "v1", Stored("stored", "v1"), Cancel)).ShouldBe((true, "stored"));
        _reads.ShouldBe(1);
    }

    /// <summary>What another process erased leaves here goes once past its lifetime (ADR 0203).</summary>
    [Fact]
    public void Texts_past_their_lifetime_are_let_go_though_never_asked_for()
    {
        var time = new FakeTimeProvider();
        var texts = new TextsRead<string>(time);
        texts.Remember("erased", "keys", "v1");
        time.Advance(TimeSpan.FromSeconds(TextsRead<string>.LifetimeSeconds));

        texts.Remember("other", "kept", "v1");

        texts.Count.ShouldBe(1);
    }

    /// <summary>What another process erased leaves here goes within two lifetimes, though the store is never used again (#1260).</summary>
    [Fact]
    public void Texts_past_their_lifetime_are_let_go_though_the_store_is_never_used_again()
    {
        var time = new FakeTimeProvider();
        var texts = new TextsRead<string>(time);
        texts.Remember("erased", "keys", "v1");

        time.Advance(TimeSpan.FromSeconds(2 * TextsRead<string>.LifetimeSeconds));

        texts.Count.ShouldBe(0);
        texts.Sweeping.ShouldBeFalse();
    }

    [Fact]
    public void A_store_that_keeps_nothing_holds_no_sweep_and_one_that_keeps_a_text_holds_one()
    {
        var texts = new TextsRead<string>(new FakeTimeProvider());
        texts.Sweeping.ShouldBeFalse();

        texts.Remember("a", "kept", "v1");

        texts.Sweeping.ShouldBeTrue();
    }

    [Fact]
    public void A_text_kept_just_now_outlives_the_sweep_that_lets_an_older_one_go()
    {
        var time = new FakeTimeProvider();
        var texts = new TextsRead<string>(time);
        texts.Remember("old", "kept", "v1");
        time.Advance(TimeSpan.FromSeconds(TextsRead<string>.LifetimeSeconds - 1));
        texts.Remember("new", "kept", "v1");

        time.Advance(TimeSpan.FromSeconds(1));

        texts.Count.ShouldBe(1);
        texts.Sweeping.ShouldBeTrue();
    }

    [Fact]
    public async Task A_text_read_while_its_document_is_deleted_is_forgotten_once_it_is_gone()
    {
        var texts = new TextsRead<string>();
        texts.Remember("a", "kept", "v1");

        await texts.ForgetAsync("a", () =>
        {
            texts.Count.ShouldBe(0);
            texts.Remember("a", "read meanwhile", "v1");
            return Task.CompletedTask;
        });

        texts.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Texts_read_while_the_documents_they_name_are_deleted_are_forgotten_once_they_are_gone()
    {
        var texts = new TextsRead<int>();
        texts.Remember(1, "kept", "v1");
        texts.Remember(3, "kept", "v1");

        await texts.ForgetAsync(day => day < 2, () =>
        {
            texts.Count.ShouldBe(1);
            texts.Remember(1, "read meanwhile", "v1");
            return Task.CompletedTask;
        });

        texts.Count.ShouldBe(1);
    }

    /// <summary>The sweep is started from a request, and must hold none of that request's context while it runs (#1260).</summary>
    [Fact]
    public void The_sweep_is_started_without_the_context_of_the_read_that_kept_the_first_text()
    {
        var time = new FlowWatchingTime();
        var texts = new TextsRead<string>(time);

        texts.Remember("a", "kept", "v1");

        time.FlowSuppressed.ShouldBe(true);
    }

    private sealed class FlowWatchingTime : TimeProvider
    {
        public bool? FlowSuppressed { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            FlowSuppressed = ExecutionContext.IsFlowSuppressed();
            return base.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
}
