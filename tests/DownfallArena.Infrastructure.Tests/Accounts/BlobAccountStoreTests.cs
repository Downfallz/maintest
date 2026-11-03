using System.Net.Sockets;
using Azure.Storage;
using Azure.Storage.Blobs;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>
/// Against the storage emulator (Azurite), as <c>BlobArtifactStoreTests</c> are: the create-if-absent and the
/// conditional delete are the service's rules, which a fake of the SDK would only restate. Skipped without one.
/// </summary>
public sealed class BlobAccountStoreTests : AccountStoreContract
{
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private static readonly Uri Endpoint = new(Environment.GetEnvironmentVariable("AZURITE_BLOB_ENDPOINT") ?? "http://127.0.0.1:10000/devstoreaccount1");

    protected override async Task<IAccountStore> StoreAsync()
    {
        if (!await Reachable())
        {
            Assert.Skip($"No storage emulator at {Endpoint}: run `npx azurite-blob` or set AZURITE_BLOB_ENDPOINT.");
        }

        var container = new BlobContainerClient(new Uri($"{Endpoint}/accounts-{Guid.CreateVersion7():N}"), new StorageSharedKeyCredential(Account, Key));
        return await BlobAccountStore.OpenAsync(container, Cancel);
    }

    private static async Task<bool> Reachable()
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Endpoint.Host, Endpoint.Port, Cancel);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
