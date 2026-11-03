using System.Buffers;
using System.Security.Cryptography;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a player's account (ADR 0122): <c>acct_</c> and sixteen random base32 characters. It is what a
/// seat and a result are attached to, and it is never shown as a name. Drawn from the system's cryptographic
/// generator, not <see cref="Randomness.IRandomSource" />: it is a key nobody may guess, and nothing about a
/// match depends on it.
/// </summary>
public readonly record struct AccountId
{
    public const string Prefix = "acct_";
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";
    private const int Length = 16;
    private static readonly SearchValues<char> Characters = SearchValues.Create(Alphabet);

    private AccountId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static AccountId New() => new(Prefix + RandomNumberGenerator.GetString(Alphabet, Length));

    /// <summary>The id written as <paramref name="value" />, or <c>false</c> when it is not one.</summary>
    public static bool TryParse(string? value, out AccountId id)
    {
        id = default;
        if (value is null || value.Length != Prefix.Length + Length || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (value.AsSpan(Prefix.Length).ContainsAnyExcept(Characters))
        {
            return false;
        }

        id = new AccountId(value);
        return true;
    }

    public static AccountId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"'{value}' is not an account id.");

    public override string ToString() => Value;
}
