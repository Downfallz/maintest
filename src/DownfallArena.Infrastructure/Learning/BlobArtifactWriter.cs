using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Writes artifacts as blobs under one prefix of a container (ADR 0080): a JSON document is a block blob
/// replaced whole, and a JSON lines file is an append blob, which is the one kind of blob that takes a line
/// at the end without rewriting what is before it.
/// </summary>
/// <remarks>
/// The same turn-taking as <see cref="FileArtifactWriter" />, for the same reason: a table writes from
/// whichever request thread a tap arrived on, and two appends to one blob racing each other is a line lost.
/// The container is expected to exist; <see cref="BlobArtifactStore" /> makes it so.
/// </remarks>
public sealed class BlobArtifactWriter : IArtifactWriter
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();
    private static readonly BlobHttpHeaders Json = new() { ContentType = "application/json" };

    private readonly BlobContainerClient _container;
    private readonly Lock _gate = new();
    private Task _turn = Task.CompletedTask;

    public BlobArtifactWriter(BlobContainerClient container, string prefix)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
        Prefix = BlobArtifactNames.Prefix(prefix);
    }

    /// <summary>The run's prefix inside the container, with its trailing slash.</summary>
    public string Prefix { get; }

    public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var blob = _container.GetBlobClient(BlobArtifactNames.Resolve(Prefix, relativePath));
        return InTurnAsync(async () =>
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, value, ArtifactJson.DocumentOptions, cancellationToken);
            stream.Position = 0;
            await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = Json }, cancellationToken);
        });
    }

    public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var blob = _container.GetAppendBlobClient(BlobArtifactNames.Resolve(Prefix, relativePath));
        return InTurnAsync(() => blob.CreateAsync(new AppendBlobCreateOptions { HttpHeaders = Json }, cancellationToken));
    }

    public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var blob = _container.GetAppendBlobClient(BlobArtifactNames.Resolve(Prefix, relativePath));
        return InTurnAsync(async () =>
        {
            using var stream = new MemoryStream();
            foreach (var value in values)
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(value, ArtifactJson.LineOptions));
                stream.Write(NewLine);
            }

            await blob.CreateIfNotExistsAsync(new AppendBlobCreateOptions { HttpHeaders = Json }, cancellationToken);
            if (stream.Length > 0)
            {
                stream.Position = 0;
                await blob.AppendBlockAsync(stream, cancellationToken: cancellationToken);
            }
        });
    }

    private Task InTurnAsync(Func<Task> write)
    {
        lock (_gate)
        {
            var mine = AfterAsync(_turn, write);
            _turn = mine.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return mine;
        }

        static async Task AfterAsync(Task turn, Func<Task> write)
        {
            await turn;
            await write();
        }
    }
}
