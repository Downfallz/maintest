using System.Net.Sockets;
using System.Text.Json;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Tests.Learning;

/// <summary>
/// Against the storage emulator (Azurite), which CI runs as a service and a developer runs with
/// <c>npx azurite-blob</c>. Without one these tests are skipped rather than failed, and say so: the adapter
/// is not exercised by a fake of the SDK, because an append blob's rules are the SDK's, not ours.
/// </summary>
public sealed class BlobArtifactStoreTests
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    [Fact]
    public async Task A_document_is_a_blob_replaced_whole_and_read_back_by_its_path()
    {
        var store = await Store();
        var writer = store.Writer("run-1");

        await writer.WriteJsonAsync("manifest.json", new { Matches = 0 }, TestContext.Current.CancellationToken);
        await writer.WriteJsonAsync("manifest.json", new { Matches = 3, Name = "run" }, TestContext.Current.CancellationToken);

        var text = (await store.Reader("run-1").ReadTextAsync("manifest.json", TestContext.Current.CancellationToken)).ShouldNotBeNull();
        using var document = JsonDocument.Parse(text);
        document.RootElement.GetProperty("matches").GetInt32().ShouldBe(3);
        document.RootElement.GetProperty("name").GetString().ShouldBe("run");
        text.ShouldContain("\n");
    }

    [Fact]
    public async Task Lines_append_to_one_blob_and_starting_it_again_empties_it()
    {
        var store = await Store();
        var writer = store.Writer("run-2");
        var reader = store.Reader("run-2");

        await writer.AppendJsonLinesAsync("steps.jsonl", [new { Index = 0 }, new { Index = 1 }], TestContext.Current.CancellationToken);
        await writer.AppendJsonLinesAsync("steps.jsonl", [new { Index = 2 }], TestContext.Current.CancellationToken);
        await writer.AppendJsonLinesAsync("steps.jsonl", Array.Empty<object>(), TestContext.Current.CancellationToken);
        (await reader.ReadTextAsync("steps.jsonl", TestContext.Current.CancellationToken)).ShouldBe("{\"index\":0}\n{\"index\":1}\n{\"index\":2}\n");

        await writer.StartJsonLinesAsync("steps.jsonl", TestContext.Current.CancellationToken);

        (await reader.ReadTextAsync("steps.jsonl", TestContext.Current.CancellationToken)).ShouldBe("");
    }

    [Fact]
    public async Task Traces_list_under_their_prefix_in_order_and_other_runs_do_not()
    {
        var store = await Store();
        await store.Writer("run-3").WriteJsonAsync("traces/b.json", new { }, TestContext.Current.CancellationToken);
        await store.Writer("run-3").WriteJsonAsync("traces/a.json", new { }, TestContext.Current.CancellationToken);
        await store.Writer("run-3").WriteJsonAsync("manifest.json", new { }, TestContext.Current.CancellationToken);
        await store.Writer("run-30").WriteJsonAsync("traces/c.json", new { }, TestContext.Current.CancellationToken);

        (await store.Reader("run-3").ListAsync("traces", TestContext.Current.CancellationToken)).ShouldBe(["traces/a.json", "traces/b.json"]);
        (await store.Reader("run-3").ReadTextAsync("traces/c.json", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Appends_from_many_threads_lose_no_line()
    {
        var store = await Store();
        var writer = store.Writer("run-4");
        await writer.StartJsonLinesAsync("notes.jsonl", TestContext.Current.CancellationToken);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            Task.Run(() => writer.AppendJsonLinesAsync("notes.jsonl", [new { Index = index }], TestContext.Current.CancellationToken))));

        var lines = (await store.Reader("run-4").ReadTextAsync("notes.jsonl", TestContext.Current.CancellationToken)).ShouldNotBeNull().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(20);
    }

    [Theory]
    [InlineData("../other/manifest.json")]
    [InlineData("/manifest.json")]
    [InlineData("traces//a.json")]
    public async Task A_path_that_leaves_the_run_is_refused_before_any_request(string path)
    {
        var writer = new BlobArtifactWriter(new BlobContainerClient(new Uri("http://127.0.0.1:1/devstoreaccount1/nowhere")), "run");

        await Should.ThrowAsync<ArgumentException>(() => writer.WriteJsonAsync(path, new { }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_run_is_located_at_its_prefix_in_the_container()
    {
        var store = await Store();

        store.LocationOf("run-5").ShouldBe($"{store.Uri}/run-5");
    }

    private static async Task<BlobArtifactStore> Store()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Endpoint}/tests-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        return await BlobArtifactStore.OpenAsync(container, TestContext.Current.CancellationToken);
    }

    private static async Task<bool> Reachable()
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Endpoint.Host, Endpoint.Port, TestContext.Current.CancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
