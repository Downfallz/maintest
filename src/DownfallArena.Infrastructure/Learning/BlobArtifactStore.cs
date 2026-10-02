using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Runs as prefixes of one blob container (ADR 0080): what <see cref="FileArtifactStore" /> keeps as directories
/// under a root, this keeps as <c>&lt;run&gt;/&lt;path&gt;</c> blobs. Opened with <see cref="OpenAsync" />, which
/// makes sure the container exists, so a store handed out can be written to at once.
/// </summary>
public sealed class BlobArtifactStore : IArtifactStore
{
    private readonly BlobContainerClient _container;

    private BlobArtifactStore(BlobContainerClient container)
    {
        _container = container;
    }

    /// <summary>The container's URL, without a credential in it.</summary>
    public Uri Uri => _container.Uri;

    public static async Task<BlobArtifactStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobArtifactStore(container);
    }

    public IArtifactWriter Writer(string run) => new BlobArtifactWriter(_container, run);

    public IArtifactReader Reader(string run) => new BlobArtifactReader(_container, run);

    /// <summary>
    /// The runs are the first segment of every blob name, read with the container's own delimiter listing so a
    /// container of a thousand sessions is a thousand prefixes and not every blob in it.
    /// </summary>
    public async Task<IReadOnlyList<string>> RunsAsync(CancellationToken cancellationToken = default)
    {
        var runs = new List<string>();
        await foreach (var item in _container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, delimiter: "/", prefix: null, cancellationToken))
        {
            if (item.IsPrefix)
            {
                runs.Add(item.Prefix.TrimEnd('/'));
            }
        }

        runs.Sort(StringComparer.Ordinal);
        return runs;
    }

    public async Task<bool> DeleteAsync(string run, CancellationToken cancellationToken = default)
    {
        var prefix = BlobArtifactNames.Prefix(run);
        var any = false;
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            await _container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
            any = true;
        }

        return any;
    }

    public string LocationOf(string run) => $"{_container.Uri}/{BlobArtifactNames.Prefix(run).TrimEnd('/')}";
}
