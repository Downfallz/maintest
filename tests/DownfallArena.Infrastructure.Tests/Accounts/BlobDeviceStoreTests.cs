using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>Against the storage emulator (Azurite), as the other blob stores are; skipped without one.</summary>
public sealed class BlobDeviceStoreTests : DeviceStoreContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private BlobContainerClient? _container;

    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    protected override async Task<IDeviceStore> StoreAsync()
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

        _container = new BlobContainerClient(new Uri($"{Endpoint}/devices-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        return await BlobDeviceStore.OpenAsync(_container, Cancel);
    }

    /// <summary>A list another revision wrote, with fields this one does not know: adding a device keeps them (#1211).</summary>
    [Fact]
    public async Task Changing_a_list_keeps_the_fields_this_revision_does_not_know()
    {
        var store = await StoreAsync();
        var account = SharedKernel.Identifiers.AccountId.New();
        var blob = _container!.GetBlobClient($"devices/{account.Value}.json");
        await blob.UploadAsync(BinaryData.FromString(DeviceFields.Written), Cancel);

        var read = await store.ReadAsync(account, Cancel);
        (await store.SaveAsync(account, DeviceFields.Added(read.Devices), read.Version, Cancel)).ShouldBeTrue();

        DeviceFields.ShouldKeepThem((await blob.DownloadContentAsync(Cancel)).Value.Content.ToString());
    }
}
