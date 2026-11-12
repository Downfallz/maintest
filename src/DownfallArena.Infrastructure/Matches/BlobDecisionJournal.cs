using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DownfallArena.Application.Matches.Journaling;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Infrastructure.Learning;

namespace DownfallArena.Infrastructure.Matches;

/// <summary>
/// Journals as append blobs, <c>&lt;run&gt;/decisions.jsonl</c> in a container (ADR 0080): the blob the table
/// has written since ADR 0091. An append is conditional on the blob's length, which the service checks as it
/// takes the block, so two replicas cannot both write the same line (ADR 0159).
/// </summary>
public sealed class BlobDecisionJournal : IDecisionJournal
{
    private static readonly BlobHttpHeaders Json = new() { ContentType = "application/json" };

    private readonly BlobContainerClient _container;
    private readonly TimeProvider _clock;

    /// <param name="container">The container the runs are kept in. It is expected to exist, as <see cref="BlobArtifactStore" /> makes it.</param>
    public BlobDecisionJournal(BlobContainerClient container, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(clock);
        _container = container;
        _clock = clock;
    }

    public async Task<JournalReading> ReadAsync(string run, CancellationToken cancellationToken = default)
    {
        try
        {
            var download = await _container.GetBlobClient(NameOf(run)).DownloadContentAsync(cancellationToken);
            return JournalLines.Read(download.Value.Content.ToMemory().Span);
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            return JournalReading.Empty;
        }
    }

    public async Task<JournalReading?> ReadAfterAsync(string run, JournalPosition after, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        var blob = _container.GetBlobClient(NameOf(run));
        try
        {
            // The bytes from the mark on, the service's to cut: a range is read as the blob stands when it is
            // asked, and every append is a whole block of whole lines.
            var download = await blob.DownloadContentAsync(new BlobDownloadOptions { Range = new HttpRange(after.Mark) }, cancellationToken);
            return JournalLines.Read(download.Value.Content.ToMemory().Span, after);
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            return after.Mark == 0 ? new JournalReading([], after) : null;
        }
        catch (RequestFailedException failure) when (failure.Status == 416)
        {
            // A range that starts at the end, or past it: nothing written since, or not this journal.
            return await EndAsync(run, cancellationToken) == after.Mark ? new JournalReading([], after) : null;
        }
    }

    public async Task<JournalPosition?> AppendAsync(string run, JournalPosition after, IReadOnlyList<IJournalEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(entries);
        var blob = _container.GetAppendBlobClient(NameOf(run));
        await blob.CreateIfNotExistsAsync(new AppendBlobCreateOptions { HttpHeaders = Json }, cancellationToken);
        if (entries.Count == 0)
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength == after.Mark ? after : null;
        }

        var lines = JournalLines.Write(after, entries, _clock.GetUtcNow());
        try
        {
            using var stream = new MemoryStream(lines);
            await blob.AppendBlockAsync(
                stream,
                new AppendBlobAppendBlockOptions { Conditions = new AppendBlobRequestConditions { IfAppendPositionEqual = after.Mark } },
                cancellationToken);
        }
        catch (RequestFailedException failure) when (failure.Status == 412)
        {
            return null;
        }

        return new JournalPosition(after.Lines + entries.Count, after.Mark + lines.Length);
    }

    public async Task<long> EndAsync(string run, CancellationToken cancellationToken = default)
    {
        try
        {
            var properties = await _container.GetAppendBlobClient(NameOf(run)).GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            return 0;
        }
    }

    public async Task<JournalWrite?> LastWrittenAsync(string run, CancellationToken cancellationToken = default)
    {
        try
        {
            var properties = await _container.GetAppendBlobClient(NameOf(run)).GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength == 0 ? null : new JournalWrite(properties.Value.LastModified, properties.Value.ContentLength);
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            return null;
        }
    }

    private static string NameOf(string run) => BlobArtifactNames.Resolve(BlobArtifactNames.Prefix(run), JournalLines.FileName);
}
