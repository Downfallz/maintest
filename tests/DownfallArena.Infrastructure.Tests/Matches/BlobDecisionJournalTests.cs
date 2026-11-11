using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Blobs;
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

    private static async Task<BlobDecisionJournal> Journal()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Endpoint}/tests-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        await BlobArtifactStore.OpenAsync(container, TestContext.Current.CancellationToken);
        return new BlobDecisionJournal(container, TimeProvider.System);
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
