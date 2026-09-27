using Azure.Storage.Blobs;
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

    public string LocationOf(string run) => $"{_container.Uri}/{BlobArtifactNames.Prefix(run).TrimEnd('/')}";
}
