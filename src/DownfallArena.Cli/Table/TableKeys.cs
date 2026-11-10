using System.Security.Cryptography;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The keys of a table -- a seat's token, an invitation, the pilot's token -- of the form <c>&lt;table&gt;.&lt;secret&gt;</c>
/// (ADR 0159, phase 4.3): a host that does not hold the table reads which one it is off the key, with no index
/// to keep beside it. The secret is what makes it a key; the table's id is no secret, a page names it in its address.
/// </summary>
/// <remarks>
/// A key minted before is 32 hex digits and names no table. It keeps working for the life of its table, found
/// the way it always was.
/// </remarks>
internal static class TableKeys
{
    private const char Separator = '.';

    /// <summary>A new key of a table: its id, then 128 random bits, which no other browser tab can guess.</summary>
    public static string For(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        if (table.Contains(Separator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A table's id cannot hold '{Separator}', which ends it in its keys.", nameof(table));
        }

        return $"{table}{Separator}{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
    }

    /// <summary>The table a key names, or none for a key minted before keys named their table.</summary>
    public static string? TableOf(string? key)
    {
        var end = key?.LastIndexOf(Separator) ?? -1;
        return end > 0 && end < key!.Length - 1 ? key[..end] : null;
    }
}
