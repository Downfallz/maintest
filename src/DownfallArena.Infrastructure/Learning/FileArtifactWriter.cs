using System.Text.Json;
using DownfallArena.Application.Learning.Ports;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// Writes artifacts as files under one root directory, creating directories as needed. Paths stay inside the
/// root: a relative path that escapes it is refused.
/// </summary>
/// <remarks>
/// One writer serializes its own writes. A batch only ever writes from the thread running it, but a playtest
/// table writes from whichever request thread a tap arrived on (ADR 0054), and two appends racing on one file
/// do not fail: on Unix the share mode is advisory, so the second stream opens at a length the first has
/// already moved past and a line is silently lost. Silently losing a note is the one thing a session recording
/// must not do, so the lock is here rather than at each caller. Two writers over the same directory would
/// still race, which is why a run directory has one.
/// </remarks>
public sealed class FileArtifactWriter : IArtifactWriter
{
    private static readonly byte[] NewLine = "\n"u8.ToArray();

    private readonly Lock _gate = new();

    // The process's, not the writer's: two tables' hosts over one directory are two writers over the same files,
    // and a conditional write is a read and a write that nothing may come between.
    private static readonly Lock Conditional = new();

    // The tail of the chain: a task that completes when the write before this one has finished, whether it
    // succeeded or not. A turn, not a result -- a failed write must not fail the next one.
    private Task _turn = Task.CompletedTask;

    public FileArtifactWriter(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public Task WriteJsonAsync<TValue>(string relativePath, TValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var path = Resolve(relativePath);
        return InTurnAsync(async () =>
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            await JsonSerializer.SerializeAsync(stream, value, ArtifactJson.DocumentOptions, cancellationToken);
        });
    }

    /// <remarks>
    /// Written beside the document and moved over it, so a reader finds the content before or the content
    /// after and never half of it. Two processes over one directory are not kept apart here, as with a table's
    /// journal on disk: a directory store is one process's.
    /// </remarks>
    public Task<string?> WriteJsonIfAsync<TValue>(string relativePath, TValue value, string? expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var path = Resolve(relativePath);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ArtifactJson.DocumentOptions);
        var written = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var turn = InTurnAsync(() =>
        {
            written.TrySetResult(Replaced(path, bytes, expected));
            return Task.CompletedTask;
        });
        return AnsweredAsync(turn, written.Task);

        static async Task<string?> AnsweredAsync(Task turn, Task<string?> answer)
        {
            await turn;
            return await answer;
        }
    }

    private static string? Replaced(string path, byte[] bytes, string? expected)
    {
        lock (Conditional)
        {
            if (FileTags.Current(path) != expected)
            {
                return null;
            }

            var beside = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(beside, bytes);
            File.Move(beside, path, overwrite: true);
            return FileTags.Of(bytes);
        }
    }

    public Task StartJsonLinesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        return InTurnAsync(async () =>
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            await stream.FlushAsync(cancellationToken);
        });
    }

    public Task AppendJsonLinesAsync<TValue>(string relativePath, IEnumerable<TValue> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var path = Resolve(relativePath);
        return InTurnAsync(async () =>
        {
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            foreach (var value in values)
            {
                await stream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value, ArtifactJson.LineOptions), cancellationToken);
                await stream.WriteAsync(NewLine, cancellationToken);
            }
        });
    }

    /// <summary>
    /// Queues a write behind the one before it. A chained task rather than a semaphore, because a semaphore is
    /// a disposable this writer would then have to own and every caller would have to dispose, for a lock that
    /// lives exactly as long as the object does.
    /// </summary>
    private Task InTurnAsync(Func<Task> write)
    {
        lock (_gate)
        {
            var mine = AfterAsync(_turn, write);

            // The next writer waits for this one to be over, not for it to have worked. The exception itself
            // belongs to whoever called this, who is handed `mine` and observes it there.
            _turn = mine.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return mine;
        }

        static async Task AfterAsync(Task turn, Func<Task> write)
        {
            await turn;
            await write();
        }
    }

    private string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        // The canonical path is checked against the very prefix it was built from, the shape the path traversal
        // analysis (S2083) reads as a guard; checking it against a prefix built separately reads as no guard at all.
        var root = Path.EndsInDirectorySeparator(RootDirectory) ? RootDirectory : RootDirectory + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (Path.IsPathRooted(relativePath) || !fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Artifact path '{relativePath}' leaves the run directory.", nameof(relativePath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? RootDirectory);
        return fullPath;
    }
}
