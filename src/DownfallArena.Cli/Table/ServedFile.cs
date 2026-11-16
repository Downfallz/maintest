using System.IO.Compression;
using System.Security.Cryptography;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One of the table's files as it is served (ADR 0166): its bytes, the tag that names them, and the same bytes
/// compressed for a browser that takes Brotli or gzip. Pictures and the font are compressed already and kept as they
/// are; a text file too small to gain is kept as it is too.
/// </summary>
internal sealed record ServedFile(DateTime Written, string Tag, byte[] Raw, byte[]? Brotli, byte[]? Gzip)
{
    /// <summary>Below this, compressing costs more than it saves.</summary>
    public const int Smallest = 1024;

    public static ServedFile Read(string path, DateTime written, bool compress)
    {
        var raw = File.ReadAllBytes(path);
        var tag = $"\"{Convert.ToHexStringLower(SHA256.HashData(raw))[..32]}\"";
        return compress && raw.Length >= Smallest
            ? new ServedFile(written, tag, raw, Compressed(raw, stream => new BrotliStream(stream, CompressionLevel.Optimal)), Compressed(raw, stream => new GZipStream(stream, CompressionLevel.Optimal)))
            : new ServedFile(written, tag, raw, null, null);
    }

    /// <summary>The encoding this browser takes, Brotli first, and the bytes in it; no encoding, the raw bytes.</summary>
    public (string? Encoding, byte[] Body) For(string? acceptEncoding)
    {
        var accepted = Accepted(acceptEncoding);
        if (Brotli is not null && accepted.Contains("br"))
        {
            return ("br", Brotli);
        }

        return Gzip is not null && accepted.Contains("gzip") ? ("gzip", Gzip) : (null, Raw);
    }

    // The encodings the browser takes: each one named, unless its quality is zero, which is the browser refusing it.
    private static HashSet<string> Accepted(string? acceptEncoding)
    {
        var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (acceptEncoding ?? string.Empty).Split(','))
        {
            var parts = entry.Split(';');
            var name = parts[0].Trim();
            var refused = parts.Skip(1).Select(part => part.Trim()).Any(part =>
                part.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(part[2..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var quality)
                && quality <= 0);
            if (name.Length > 0 && !refused)
            {
                accepted.Add(name);
            }
        }

        return accepted;
    }

    private static byte[] Compressed(byte[] raw, Func<Stream, Stream> compressor)
    {
        using var buffer = new MemoryStream();
        using (var compressing = compressor(buffer))
        {
            compressing.Write(raw);
        }

        return buffer.ToArray();
    }
}
