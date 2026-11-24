using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>Against the storage emulator (Azurite), as the other blob stores are; skipped without one.</summary>
public sealed class BlobNoticeStoreTests : NoticeStoreContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private readonly BlobReads _reads = new();
    private BlobContainerClient? _container;

    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    protected override async Task<INoticeStore> StoreAsync()
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

        _container = new BlobContainerClient(new Uri($"{Endpoint}/notices-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key), _reads.Options());
        return await BlobNoticeStore.OpenAsync(_container, Cancel);
    }

    protected override async Task<INoticeStore> OtherStoreAsync(INoticeStore store) =>
        await BlobNoticeStore.OpenAsync(new BlobContainerClient(_container!.Uri, new StorageSharedKeyCredential(Account, Key)), Cancel);

    /// <summary>A log another revision wrote, with fields this one does not know: marking it read keeps them (#1211).</summary>
    [Fact]
    public async Task Changing_a_log_keeps_the_fields_this_revision_does_not_know()
    {
        var store = await StoreAsync();
        var account = SharedKernel.Identifiers.AccountId.New();
        var blob = _container!.GetBlobClient($"notices/{account.Value}.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeFields.Written), Cancel);

        var read = await store.ReadAsync(account, Cancel);
        (await store.WriteAsync(account, NoticeFields.Read(read.Entries), read.Version, Cancel)).ShouldBeTrue();

        NoticeFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }

    /// <summary>A write over the version just read keeps the fields this revision does not know without a second GET (#1232).</summary>
    [Fact]
    public async Task Writing_over_the_version_read_reads_the_log_no_second_time()
    {
        var store = await StoreAsync();
        var account = SharedKernel.Identifiers.AccountId.New();
        var blob = _container!.GetBlobClient($"notices/{account.Value}.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeFields.Written), Cancel);
        var read = await store.ReadAsync(account, Cancel);
        _reads.Reset();

        (await store.WriteAsync(account, NoticeFields.Read(read.Entries), read.Version, Cancel)).ShouldBeTrue();

        _reads.Count.ShouldBe(0);
        NoticeFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }

    /// <summary>A store that did not read the log itself, another process's, reads it again to keep those fields.</summary>
    [Fact]
    public async Task Writing_over_a_version_another_store_read_still_keeps_the_fields_this_revision_does_not_know()
    {
        var store = await StoreAsync();
        var account = SharedKernel.Identifiers.AccountId.New();
        var blob = _container!.GetBlobClient($"notices/{account.Value}.json");
        await blob.UploadAsync(BinaryData.FromString(NoticeFields.Written), Cancel);
        var read = await store.ReadAsync(account, Cancel);
        var other = await OtherStoreAsync(store);

        (await other.WriteAsync(account, NoticeFields.Read(read.Entries), read.Version, Cancel)).ShouldBeTrue();

        NoticeFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }
}
