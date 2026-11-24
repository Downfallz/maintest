using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>Counts the reads a blob store sends, once a call whatever its retries: what a write costs in round trips (#1232).</summary>
internal sealed class BlobReads : HttpPipelineSynchronousPolicy
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    /// <summary>Options for a container client whose every call this counts.</summary>
    public BlobClientOptions Options()
    {
        var options = new BlobClientOptions();
        options.AddPolicy(this, HttpPipelinePosition.PerCall);
        return options;
    }

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override void OnSendingRequest(HttpMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Request.Method == RequestMethod.Get || message.Request.Method == RequestMethod.Head)
        {
            Interlocked.Increment(ref _count);
        }
    }
}
