using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>Results as blobs under <c>results/</c> in the accounts' container (ADR 0122, stage 5).</summary>
public sealed class BlobMatchResults : IMatchResults
{
    private readonly BlobContainerClient _container;

    private BlobMatchResults(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobMatchResults> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobMatchResults(container);
    }

    public async Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default) =>
        await _container.GetBlobClient(ResultDocuments.PathOf(result)).UploadAsync(
            BinaryData.FromString(ResultDocuments.Write(result)),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);

    public async Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<MatchResult>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, ResultDocuments.Prefix, cancellationToken))
        {
            var download = await _container.GetBlobClient(item.Name).DownloadContentAsync(cancellationToken);
            results.Add(ResultDocuments.Read(download.Value.Content.ToString()));
        }

        return [.. results.OrderBy(result => result.FinishedAt).ThenBy(result => result.MatchId, StringComparer.Ordinal)];
    }
}
