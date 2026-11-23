using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// A file read with a version, the hash of what it holds, and written only over that version (ADR 0201), the check
/// and the move made under a lock file of the directory, so a table and a <c>table-tend</c> run on one laptop never
/// interleave them.
/// </summary>
internal static class ConditionalFile
{
    private const string LockName = ".lock";

    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(10);

    /// <summary>The file's text and its version, or none for a file that is not there.</summary>
    public static async Task<(string? Text, string? Version)> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var text = await TextAsync(path, cancellationToken);
        return (text, text is null ? null : VersionOf(text));
    }

    /// <summary>
    /// The file's text, its version and when it was last written, or none for a file that is not there. The time is
    /// read after the text: a write between the two makes it later, never earlier, than the text read.
    /// </summary>
    public static async Task<(string? Text, string? Version, DateTimeOffset? Written)> ReadWrittenAsync(string path, CancellationToken cancellationToken)
    {
        var (text, version) = await ReadAsync(path, cancellationToken);
        return text is null ? (null, null, null) : (text, version, new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
    }

    /// <summary>
    /// Writes the text beside its place and moves it there, or deletes the file for none, only if it is still at that
    /// version. False when another write came first.
    /// </summary>
    public static async Task<bool> WriteAsync(string path, string? text, string? version, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        await using var held = await LockedAsync(directory, cancellationToken);
        var now = await TextAsync(path, cancellationToken);
        if ((now is null ? null : VersionOf(now)) != version)
        {
            return false;
        }

        if (text is null)
        {
            File.Delete(path);
            return true;
        }

        var staged = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(staged, text, cancellationToken);
        File.Move(staged, path, overwrite: true);
        return true;
    }

    /// <summary>
    /// Deletes the file under the directory's lock, whatever its version, so a write that passed its check cannot move
    /// its file back in after; a file or a directory that is not there is nothing to delete.
    /// </summary>
    public static async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory))
        {
            return;
        }

        await using var held = await LockedAsync(directory, cancellationToken);
        File.Delete(path);
    }

    /// <summary>
    /// The directory's lock file, held alone; it is left in place, since a file deleted while another process waits on
    /// it would let two writers each hold a lock of their own.
    /// </summary>
    private static async Task<FileStream> LockedAsync(string directory, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + LockWait;
        while (true)
        {
            try
            {
                return new FileStream(Path.Combine(directory, LockName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < until)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }
    }

    private static async Task<string?> TextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static string VersionOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
