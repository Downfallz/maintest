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
        return Reserved.Contains(name.Key) ? Result.Failure<Username>(AccountErrors.UsernameReserved) : Result.Success(name);
    }

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
