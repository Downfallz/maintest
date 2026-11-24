using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Tests.Accounts;
using DownfallArena.Infrastructure.Work;

namespace DownfallArena.Infrastructure.Tests.Work;

/// <summary>Against the storage emulator (Azurite), as the other blob stores are; skipped without one.</summary>
public sealed class BlobNoticeCountStoreTests : NoticeCountStoreContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private readonly BlobReads _reads = new();
    private BlobContainerClient? _container;

    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    protected override async Task<INoticeCountStore> StoreAsync()
    {
        using (var client = new TcpClient())
        {
            try
            {
                await client.ConnectAsync(Endpoint.Host, Endpoint.Port, Cancel);
            }
            catch (SocketException)
            {
                Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
            }
        }

        _container = new BlobContainerClient(new Uri($"{Endpoint}/notice-counts-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key), _reads.Options());
        return await BlobNoticeCountStore.OpenAsync(_container, Cancel);
    }

    protected override async Task<INoticeCountStore> OtherStoreAsync(INoticeCountStore store) =>
        await BlobNoticeCountStore.OpenAsync(new BlobContainerClient(_container!.Uri, new StorageSharedKeyCredential(Account, Key)), Cancel);

    /// <summary>A day another revision wrote, with a count this one does not know: adding to it keeps that count (#1151).</summary>
    [Fact]
    public async Task Adding_to_a_day_keeps_the_fields_this_revision_does_not_know()
    {
        var store = await StoreAsync();
        var blob = _container!.GetBlobClient("2026-11-23.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeCountFields.Written), Cancel);

        var read = await store.ReadAsync(NoticeCountFields.Day, Cancel);
        (await store.WriteAsync(NoticeCountFields.Day, NoticeCount.Sum([.. read.Counts, new("turn", 1, 0, 0)]), read.Version, Cancel)).ShouldBeTrue();

        NoticeCountFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
        (await store.ReadAsync(NoticeCountFields.Day, Cancel)).Counts.ShouldBe([new NoticeCount("turn", 3, 0, 1)]);
    }

    /// <summary>Adding over the version just read keeps the count this revision does not know without a second GET (#1232).</summary>
    [Fact]
    public async Task Adding_over_the_version_read_reads_the_day_no_second_time()
    {
        var store = await StoreAsync();
        var blob = _container!.GetBlobClient("2026-11-23.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeCountFields.Written), Cancel);
        var read = await store.ReadAsync(NoticeCountFields.Day, Cancel);
        _reads.Reset();

        (await store.WriteAsync(NoticeCountFields.Day, NoticeCount.Sum([.. read.Counts, new("turn", 1, 0, 0)]), read.Version, Cancel)).ShouldBeTrue();

        _reads.Count.ShouldBe(0);
        NoticeCountFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }

    /// <summary>A store that did not read the day itself, another process's, reads it again to keep that count.</summary>
    [Fact]
    public async Task Adding_over_a_version_another_store_read_still_keeps_the_fields_this_revision_does_not_know()
    {
        var store = await StoreAsync();
        var blob = _container!.GetBlobClient("2026-11-23.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeCountFields.Written), Cancel);
        var read = await store.ReadAsync(NoticeCountFields.Day, Cancel);
        var other = await OtherStoreAsync(store);

        (await other.WriteAsync(NoticeCountFields.Day, NoticeCount.Sum([.. read.Counts, new("turn", 1, 0, 0)]), read.Version, Cancel)).ShouldBeTrue();

        NoticeCountFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }
}
