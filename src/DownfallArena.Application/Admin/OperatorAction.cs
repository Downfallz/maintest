using System.Globalization;
using System.Security.Cryptography;

namespace DownfallArena.Application.Admin;

/// <summary>
/// One thing an operator did from the admin panel (docs/tabletop/admin-plan.md, A6): who, what, on which id, when, and
/// a few words of detail. <see cref="Action" /> is one of <see cref="OperatorActions" />' stable names.
/// </summary>
public sealed record OperatorAction(string Id, DateTimeOffset At, string By, string Action, string? Target, string? Detail)
{
    /// <summary>How many actions are kept: the oldest go past it (the owner's choice, 2026-11-14).</summary>
    public const int Kept = 1000;

    private const string AtFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    /// <summary>An id for an action taken then: its time first, so ids sort as the actions were taken, then a few random characters.</summary>
    public static string NewId(DateTimeOffset at) =>
        $"{at.UtcDateTime.ToString(AtFormat, CultureInfo.InvariantCulture)}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(3))}";

    /// <summary>Whether an id is one an action could have: what a store checks before it names a file or a blob after it.</summary>
    public static bool IsId(string? id) =>
        id is { Length: 26 }
        && id[19] == '-'
        && id[20..].All(char.IsAsciiHexDigitLower)
        && DateTimeOffset.TryParseExact(id[..19], AtFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);
}
