using System.Net.Sockets;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Infrastructure.Learning;
using DownfallArena.Infrastructure.Matches;

namespace DownfallArena.Infrastructure.Tests.Matches;

/// <summary>
/// Against the storage emulator (Azurite), which CI runs as a service and a developer runs with
/// <c>npx azurite-blob</c>; skipped without one. The condition an append is made on is the service's to keep,
/// so it is the service these ask.
/// </summary>
public sealed class BlobDecisionJournalTests
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    [Fact]
    public async Task A_journal_nobody_wrote_is_empty_and_ends_at_the_start()
    {
        var journal = await Journal();

        var reading = await journal.ReadAsync("run-1", TestContext.Current.CancellationToken);

        reading.Entries.ShouldBeEmpty();
        reading.Position.ShouldBe(JournalPosition.Start);
        reading.Absent.ShouldBeTrue();
    }

    /// <summary>A blob started empty, as a recorded table's is, reads as present; once deleted, it reads as absent, whole or from the position it was read at (#874).</summary>
    [Fact]
    public async Task A_journal_started_empty_is_there_and_one_deleted_since_is_absent_from_where_it_was_read()
    {
        var (journal, container) = await Opened();
        var blob = container.GetAppendBlobClient("run-1/decisions.jsonl");
        await blob.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var started = await journal.ReadAsync("run-1", TestContext.Current.CancellationToken);
        var same = await journal.ReadAfterAsync("run-1", started.Position, TestContext.Current.CancellationToken);
        await blob.DeleteAsync(cancellationToken: TestContext.Current.CancellationToken);

        var after = await journal.ReadAfterAsync("run-1", started.Position, TestContext.Current.CancellationToken);
        var whole = await journal.ReadAsync("run-1", TestContext.Current.CancellationToken);

        started.Absent.ShouldBeFalse();
        same.ShouldNotBeNull().Absent.ShouldBeFalse();
        after.ShouldNotBeNull().Absent.ShouldBeTrue();
        whole.Absent.ShouldBeTrue();
    }

    /// <summary>
    /// A read from the start of a journal is one call, the whole blob, and never a range answered 416 and then an end
    /// asked: no delete can fall between two calls and leave an empty blob deleted since read as there (#874).
    /// </summary>
    [Fact]
    public async Task A_journal_read_from_its_start_is_one_call_and_asks_no_range()
    {
        var (_, container) = await Opened();
        await container.GetAppendBlobClient("run-1/decisions.jsonl").CreateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var asked = new Asked();
        var options = new BlobClientOptions();
        options.AddPolicy(asked, HttpPipelinePosition.PerCall);
        var journal = new BlobDecisionJournal(new BlobContainerClient(container.Uri, new StorageSharedKeyCredential(Account, Key), options), TimeProvider.System);

        var after = await journal.ReadAfterAsync("run-1", JournalPosition.Start, TestContext.Current.CancellationToken);

        after.ShouldNotBeNull().Absent.ShouldBeFalse();
        asked.Requests.ShouldHaveSingleItem().ShouldBe("GET");
    }

    /// <summary>
    /// A writer that read the blob there appends only into a blob the service holds (#874): one deleted since is not
    /// created again, even at its start, and one started empty takes the append.
    /// </summary>
    [Fact]
    public async Task An_append_into_an_existing_journal_is_refused_when_there_is_none_and_written_when_there_is()
    {
        var (journal, container) = await Opened();
        await container.GetAppendBlobClient("run-2/decisions.jsonl").CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var some = await journal.AppendExistingAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken);
        var none = await journal.AppendExistingAsync("run-1", JournalPosition.Start, [], TestContext.Current.CancellationToken);
        var started = await journal.AppendExistingAsync("run-2", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken);

        some.ShouldBeNull();
        none.ShouldBeNull();
        (await container.GetAppendBlobClient("run-1/decisions.jsonl").ExistsAsync(TestContext.Current.CancellationToken)).Value.ShouldBeFalse();
        started.ShouldNotBeNull();
        (await journal.ReadAsync("run-2", TestContext.Current.CancellationToken)).Entries.ShouldBe([DecisionJournalTests.EveryKind[0]]);
    }

    [Fact]
    public async Task Where_a_journal_ends_is_read_without_reading_it_and_is_where_the_last_append_left_it()
    {
        var journal = await Journal();
        var nothing = await journal.EndAsync("run-1", TestContext.Current.CancellationToken);
        var written = (await journal.AppendAsync("run-1", JournalPosition.Start, [.. DecisionJournalTests.EveryKind.Take(3)], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var end = await journal.EndAsync("run-1", TestContext.Current.CancellationToken);

        nothing.ShouldBe(0);
        end.ShouldBe(written.Mark);
    }

    [Fact]
    public async Task Its_last_write_is_read_with_where_the_journal_ended_after_it_and_a_journal_nobody_wrote_has_none()
    {
        var journal = await Journal();
        var nothing = await journal.LastWrittenAsync("run-1", TestContext.Current.CancellationToken);
        var written = (await journal.AppendAsync("run-1", JournalPosition.Start, [.. DecisionJournalTests.EveryKind.Take(3)], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var last = await journal.LastWrittenAsync("run-1", TestContext.Current.CancellationToken);

        nothing.ShouldBeNull();
        last.ShouldNotBeNull().Mark.ShouldBe(written.Mark);
    }

    [Fact]
    public async Task Decisions_are_read_back_in_order_and_the_end_read_is_the_end_written()
    {
        var journal = await Journal();
        var first = (await journal.AppendAsync("run-1", JournalPosition.Start, [.. DecisionJournalTests.EveryKind.Take(5)], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var second = await journal.AppendAsync("run-1", first, [.. DecisionJournalTests.EveryKind.Skip(5)], TestContext.Current.CancellationToken);

        var reading = await journal.ReadAsync("run-1", TestContext.Current.CancellationToken);
        reading.Entries.ShouldBe(DecisionJournalTests.EveryKind);
        reading.Position.ShouldBe(second);
    }

    [Fact]
    public async Task A_writer_that_read_before_another_wrote_is_refused_and_writes_nothing()
    {
        var journal = await Journal();
        var read = (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Position;
        await journal.AppendAsync("run-1", read, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken);

        var late = await journal.AppendAsync("run-1", read, [DecisionJournalTests.EveryKind[1]], TestContext.Current.CancellationToken);

        late.ShouldBeNull();
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Entries.ShouldBe([DecisionJournalTests.EveryKind[0]]);
    }

    [Fact]
    public async Task Of_two_writers_at_the_same_end_one_writes_and_the_other_is_refused()
    {
        var journal = await Journal();
        var end = (await journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var written = await Task.WhenAll(
            journal.AppendAsync("run-1", end, [DecisionJournalTests.EveryKind[1]], TestContext.Current.CancellationToken),
            journal.AppendAsync("run-1", end, [DecisionJournalTests.EveryKind[2]], TestContext.Current.CancellationToken));

        written.Count(position => position is not null).ShouldBe(1);
        (await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Appending_nothing_leaves_the_end_where_it_was()
    {
        var journal = await Journal();
        var end = (await journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var same = await journal.AppendAsync("run-1", end, [], TestContext.Current.CancellationToken);
        var stale = await journal.AppendAsync("run-1", JournalPosition.Start, [], TestContext.Current.CancellationToken);

        same.ShouldBe(end);
        stale.ShouldBeNull();
    }

    /// <summary>A writer past the start of a blob nobody holds was writing a run since deleted: refused, and no empty blob is left (#856).</summary>
    [Fact]
    public async Task An_append_past_the_start_of_a_journal_nobody_holds_is_refused_and_brings_nothing_back()
    {
        var (journal, container) = await Opened();
        var elsewhere = (await journal.AppendAsync("run-2", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var late = await journal.AppendAsync("run-1", elsewhere, [DecisionJournalTests.EveryKind[1]], TestContext.Current.CancellationToken);
        var nothing = await journal.AppendAsync("run-1", elsewhere, [], TestContext.Current.CancellationToken);
        var none = await journal.AppendAsync("run-3", JournalPosition.Start, [], TestContext.Current.CancellationToken);

        late.ShouldBeNull();
        nothing.ShouldBeNull();
        none.ShouldBe(JournalPosition.Start);
        var left = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, TestContext.Current.CancellationToken))
        {
            left.Add(blob.Name);
        }

        left.ShouldAllBe(name => name.StartsWith("run-2", StringComparison.Ordinal), "no blob, not even an empty one");
    }

    [Fact]
    public async Task Read_after_a_position_a_journal_gives_the_entries_written_since_and_where_it_ends_now()
    {
        var journal = await Journal();
        var first = (await journal.AppendAsync("run-1", JournalPosition.Start, [.. DecisionJournalTests.EveryKind.Take(4)], TestContext.Current.CancellationToken)).ShouldNotBeNull();
        var second = await journal.AppendAsync("run-1", first, [.. DecisionJournalTests.EveryKind.Skip(4)], TestContext.Current.CancellationToken);

        var since = await journal.ReadAfterAsync("run-1", first, TestContext.Current.CancellationToken);

        since.ShouldNotBeNull().Entries.ShouldBe([.. DecisionJournalTests.EveryKind.Skip(4)]);
        since.Position.ShouldBe(second);
        since.Position.ShouldBe((await journal.ReadAsync("run-1", TestContext.Current.CancellationToken)).Position);
    }

    [Fact]
    public async Task Read_after_where_it_ends_a_journal_gives_nothing_and_the_same_position()
    {
        var journal = await Journal();
        var nobody = await journal.ReadAfterAsync("run-1", JournalPosition.Start, TestContext.Current.CancellationToken);
        var end = (await journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken)).ShouldNotBeNull();

        var since = await journal.ReadAfterAsync("run-1", end, TestContext.Current.CancellationToken);

        nobody.ShouldNotBeNull().Entries.ShouldBeEmpty();
        nobody.Position.ShouldBe(JournalPosition.Start);
        since.ShouldNotBeNull().Entries.ShouldBeEmpty();
        since.Position.ShouldBe(end);
    }

    [Fact]
    public async Task Read_after_a_position_it_does_not_reach_a_journal_gives_nothing()
    {
        var journal = await Journal();
        var longer = (await journal.AppendAsync("run-2", JournalPosition.Start, [.. DecisionJournalTests.EveryKind.Take(3)], TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await journal.AppendAsync("run-1", JournalPosition.Start, [DecisionJournalTests.EveryKind[0]], TestContext.Current.CancellationToken);

        var shorter = await journal.ReadAfterAsync("run-1", longer, TestContext.Current.CancellationToken);
        var none = await journal.ReadAfterAsync("run-3", longer, TestContext.Current.CancellationToken);

        shorter.ShouldBeNull();
        none.ShouldBeNull();
    }

    private static async Task<BlobDecisionJournal> Journal() => (await Opened()).Journal;

    /// <summary>The requests sent to the service, each by its method, and “ranged” when it asks a range.</summary>
    private sealed class Asked : HttpPipelinePolicy
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _requests = new();

        public IReadOnlyList<string> Requests => [.. _requests];

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Note(message);
            ProcessNext(message, pipeline);
        }

        public override ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Note(message);
            return ProcessNextAsync(message, pipeline);
        }

        private void Note(HttpMessage message) =>
            _requests.Enqueue(message.Request.Headers.Contains("x-ms-range") || message.Request.Headers.Contains("Range")
                ? $"{message.Request.Method} ranged"
                : message.Request.Method.ToString());
    }

    private static async Task<(BlobDecisionJournal Journal, BlobContainerClient Container)> Opened()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Endpoint}/tests-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        await BlobArtifactStore.OpenAsync(container, TestContext.Current.CancellationToken);
        return (new BlobDecisionJournal(container, TimeProvider.System), container);
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
