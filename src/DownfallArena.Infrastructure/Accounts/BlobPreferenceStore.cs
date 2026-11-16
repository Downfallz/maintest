using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>An account's game settings as one blob of the accounts' container (ADR 0140), beside the account itself.</summary>
public sealed class BlobPreferenceStore : IPreferenceStore
{
    private readonly BlobContainerClient _container;

    private BlobPreferenceStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobPreferenceStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobPreferenceStore(container);
    }

    public async Task<GamePreferences?> GetAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(PreferenceDocuments.PathOf(account), cancellationToken) is { } json ? PreferenceDocuments.Read(json) : null;

    public Task SaveAsync(AccountId account, GamePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return WriteAsync(PreferenceDocuments.PathOf(account), PreferenceDocuments.Write(preferences), cancellationToken);
    }

    public async Task<TutorialProgress> GetTutorialsAsync(AccountId account, CancellationToken cancellationToken = default) =>
        await ReadAsync(PreferenceDocuments.TutorialsPathOf(account), cancellationToken) is { } json ? PreferenceDocuments.ReadTutorials(json) : TutorialProgress.None;

    public Task SaveTutorialsAsync(AccountId account, TutorialProgress tutorials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tutorials);
        return WriteAsync(PreferenceDocuments.TutorialsPathOf(account), PreferenceDocuments.WriteTutorials(tutorials), cancellationToken);
    }

    public async Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        await _container.DeleteBlobIfExistsAsync(PreferenceDocuments.PathOf(account), cancellationToken: cancellationToken);
        await _container.DeleteBlobIfExistsAsync(PreferenceDocuments.TutorialsPathOf(account), cancellationToken: cancellationToken);
    }

    private async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _container.GetBlobClient(path).DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToString();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async Task WriteAsync(string path, string document, CancellationToken cancellationToken) =>
        await _container.GetBlobClient(path).UploadAsync(
            BinaryData.FromString(document),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
}
