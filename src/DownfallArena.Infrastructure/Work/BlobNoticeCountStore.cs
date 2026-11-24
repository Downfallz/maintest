using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Work;
using DownfallArena.Infrastructure.Accounts;

namespace DownfallArena.Infrastructure.Work;

/// <summary>The notices' counts as one blob a day of a container of their own (N6), written only over the tag it was read at.</summary>
public sealed class BlobNoticeCountStore : INoticeCountStore
{
    private readonly BlobContainerClient _container;
    private readonly TextsRead<DateOnly> _read = new();

    private BlobNoticeCountStore(BlobContainerClient container)
    {
        _container = container;
    }

    public static async Task<BlobNoticeCountStore> OpenAsync(BlobContainerClient container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return new BlobNoticeCountStore(container);
    }

    public async Task<NoticeCountRead> ReadAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var (text, version) = await ConditionalBlob.ReadAsync(Blob(day), cancellationToken);
        _read.Remember(day, text, version);
        return new NoticeCountRead(text is null ? [] : NoticeCountDocuments.Read(text), version);
    }

    /// <summary>
    /// Writes back what this revision does not know of the day (#1151), from the text read at that version: kept from
    /// this store's read, or read again (#1232). A write over a version that has moved since is refused: without being
    /// tried when the day was read again, by its condition when the text was kept.
    /// </summary>
    public async Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return await _read.WriteAsync(
            day,
            version,
            before => NoticeCountDocuments.Write(counts, before),
            token => ConditionalBlob.ReadAsync(Blob(day), token),
            (text, token) => ConditionalBlob.WriteAsync(Blob(day), text, version, token),
            cancellationToken);
    }

    public async Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        _read.Forget(kept => kept < day);
        var old = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken).Where(item => NoticeCountDocuments.DayOf(item.Name) < day))
        {
            old.Add(item.Name);
        }

        foreach (var name in old)
        {
            await _container.DeleteBlobIfExistsAsync(name, cancellationToken: cancellationToken);
        }
    }

    private BlobClient Blob(DateOnly day) => _container.GetBlobClient(NoticeCountDocuments.NameOf(day));
}
