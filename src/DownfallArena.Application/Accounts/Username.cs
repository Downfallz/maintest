using System.Buffers;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The name a player chose, shown on every table they sit at (ADR 0122). Displayed as typed, unique without
/// regard to case: <see cref="Key" /> is what two names are compared by. Only ASCII is allowed, so lower-casing is
/// the whole of the comparison and no two names that look alike can differ by a character nobody sees.
/// </summary>
public sealed record Username
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 20;

    private static readonly SearchValues<char> Letters = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    private static readonly SearchValues<char> GuestCode = SearchValues.Create("23456789ABCDEFGHJKMNPQRSTUVWXYZ");
    private static readonly SearchValues<char> Allowed = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-");

    // Words a card or a page already means something by, and every agent the lobby or the admin panel can
    // seat, so a person is never mistaken for a bot, for an empty place, or for "you".
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "administrator", "operator", "pilot", "system", "support", "moderator", "guest", "anonymous",
        "nobody", "you", "me", "open", "player", "player1", "player2", "bot", "bots", "downfall", "downfallarena", "downfallinstinct",
        "automa", "random", "greedy", "lookahead", "minimax", "heuristic", "policy", "explore", "opening", "clone",
    };

    private Username(string value)
    {
        Value = value;
        Key = value.ToLowerInvariant();
    }

    /// <summary>The name as the player typed it.</summary>
    public string Value { get; }

    /// <summary>The name lower-cased: two names with the same key are the same name.</summary>
    public string Key { get; }

    /// <summary>A name a player asks for, checked against every rule; surrounding spaces are not part of it.</summary>
    public static Result<Username> Create(string? candidate)
    {
        var value = candidate?.Trim() ?? string.Empty;
        if (value.Length is < MinimumLength or > MaximumLength || !Letters.Contains(value[0]) || value.AsSpan().ContainsAnyExcept(Allowed))
        {
            return Result.Failure<Username>(AccountErrors.UsernameInvalid);
        }

        var name = new Username(value);
        return Reserved.Contains(name.Key) || IsGuestName(name.Key) ? Result.Failure<Username>(AccountErrors.UsernameReserved) : Result.Success(name);
    }

    /// <summary>What every guest's name starts with (ADR 0207); no player types a name that does.</summary>
    public const string GuestPrefix = "Guest-";

    /// <summary>The letters and digits a guest's code is drawn from: none that reads as another (0 and O, 1, I and L).</summary>
    public const string GuestAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    /// <summary>How many characters a guest's code has.</summary>
    public const int GuestCodeLength = 4;

    /// <summary>
    /// A guest's name, drawn by the host (ADR 0207): <see cref="GuestPrefix" /> and a code of <see cref="GuestCodeLength" />
    /// characters of <see cref="GuestAlphabet" />, such as <c>Guest-7KQ2</c>. Reserved: <see cref="Create" /> refuses it.
    /// </summary>
    public static Username Guest(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length != GuestCodeLength || code.AsSpan().ContainsAnyExcept(GuestCode))
        {
            throw new ArgumentException($"A guest's code is {GuestCodeLength} characters of {GuestAlphabet}.", nameof(code));
        }

        return new Username(GuestPrefix + code);
    }

    // Guest-7KQ2 and guest_7kq2 alike: one look-alike typed by a player would pass for a guest's.
    private static bool IsGuestName(string key) => key.StartsWith("guest-", StringComparison.Ordinal) || key.StartsWith("guest_", StringComparison.Ordinal);

    /// <summary>
    /// A name a store already holds. Not checked against today's rules: a rule added later must not make an
    /// account that was valid when it registered unreadable.
    /// </summary>
    public static Username Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Username(value);
    }

    public override string ToString() => Value;
}
