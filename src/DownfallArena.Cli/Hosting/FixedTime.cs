using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Cli.Hosting;

/// <summary>
/// How a host compares a token or a key it was handed with the one it holds (#862): in fixed time, because an
/// ordinal comparison stops at the first character that differs, and its time tells how much of a guess is right.
/// </summary>
internal static class FixedTime
{
    /// <summary>Whether the token supplied is the one held. A blank one is never anybody's.</summary>
    public static bool Same(string? supplied, string? held) =>
        !string.IsNullOrWhiteSpace(supplied) && !string.IsNullOrWhiteSpace(held)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(held));
}
