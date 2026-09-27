using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>Reads back what a <see cref="BlobArtifactWriter" /> wrote under one prefix of a container.</summary>
public sealed class BlobArtifactReader : IArtifactReader
{
    private readonly BlobContainerClient _container;

    public BlobArtifactReader(BlobContainerClient container, string prefix)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
        Prefix = BlobArtifactNames.Prefix(prefix);
    }

    /// <summary>The run's prefix inside the container, with its trailing slash.</summary>
    public string Prefix { get; }

    public async Task<IReadOnlyList<string>> ListAsync(string relativePrefix, CancellationToken cancellationToken = default)
    {
        var under = BlobArtifactNames.Resolve(Prefix, relativePrefix) + "/";
        var names = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, under, cancellationToken))
        {
            names.Add(item.Name[Prefix.Length..]);
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(BlobArtifactNames.Resolve(Prefix, relativePath));
        try
        {
            var content = await blob.DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToString();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}
