using System.Security.Cryptography;

namespace DownfallArena.Infrastructure.Learning;

/// <summary>
/// The tag of a document on disk: a digest of its bytes. A file has no version of its own a writer could be
/// refused on, and the documents written under a condition are a table's record, small enough to read whole.
/// </summary>
internal static class FileTags
{
    public static string Of(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The tag of the file as it is now, or none when there is no file.</summary>
    public static string? Current(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
