using DownfallArena.Infrastructure.Accounts;

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
}
