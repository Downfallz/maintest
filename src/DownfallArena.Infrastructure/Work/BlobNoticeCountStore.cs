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
        return new NoticeCountRead(text is null ? [] : NoticeCountDocuments.Read(text), version);
    }

    public Task<bool> WriteAsync(DateOnly day, IReadOnlyList<NoticeCount> counts, string? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return ConditionalBlob.WriteAsync(Blob(day), NoticeCountDocuments.Write(counts), version, cancellationToken);
    }

    public async Task ForgetBeforeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var old = new List<string>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: null, cancellationToken))
        {
            if (NoticeCountDocuments.DayOf(item.Name) < day)
            {
                old.Add(item.Name);
            }
        }

        foreach (var name in old)
        {
            await _container.DeleteBlobIfExistsAsync(name, cancellationToken: cancellationToken);
        }
    }

    private BlobClient Blob(DateOnly day) => _container.GetBlobClient(NoticeCountDocuments.NameOf(day));
}
