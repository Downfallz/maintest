using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// A blob read with its tag and written only over that tag (ADR 0201): <c>If-Match</c> over a version read,
/// <c>If-None-Match: *</c> where none was. What an account's devices and its notice log are written with, since the
/// API, the worker and the hourly job all write them.
/// </summary>
internal static class ConditionalBlob
{
    /// <summary>The blob's text and its tag, or none for a blob that is not there.</summary>
    public static async Task<(string? Text, string? Version)> ReadAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        var (text, version, _) = await ReadWrittenAsync(blob, cancellationToken);
        return (text, version);
    }

    /// <summary>The blob's text, its tag and when it was last written, all from the one download; none for a blob that is not there.</summary>
    public static async Task<(string? Text, string? Version, DateTimeOffset? Written)> ReadWrittenAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            var download = await blob.DownloadContentAsync(cancellationToken);
            return (download.Value.Content.ToString(), download.Value.Details.ETag.ToString(), download.Value.Details.LastModified);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return (null, null, null);
        }
    }

    /// <summary>
    /// Writes the text, or deletes the blob for none, only if it is still at that version. False when another write
    /// came first.
    /// </summary>
    public static async Task<bool> WriteAsync(BlobClient blob, string? text, string? version, CancellationToken cancellationToken)
    {
        try
        {
            if (text is not null)
            {
                await blob.UploadAsync(
                    BinaryData.FromString(text),
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                        Conditions = version is null ? new BlobRequestConditions { IfNoneMatch = ETag.All } : new BlobRequestConditions { IfMatch = new ETag(version) },
                    },
                    cancellationToken);
                return true;
            }

            if (version is null)
            {
                // Nothing there and nothing to keep: a delete would remove a blob another process made meanwhile.
                return !(await blob.ExistsAsync(cancellationToken)).Value;
            }

            await blob.DeleteAsync(conditions: new BlobRequestConditions { IfMatch = new ETag(version) }, cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 409 or 412)
        {
            return false;
        }
    }
}
