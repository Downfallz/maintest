using System.Collections.Immutable;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// The ways into one account (ADR 0204): one or more identities, each from a provider the host lists, in the order
/// they were added -- the first is the one the account registered with. Never empty and never one twice; two
/// lists are equal when they hold the same identities in the same order.
/// </summary>
public sealed record AccountIdentities
{
    private readonly ImmutableArray<ExternalIdentity> _all;

    private AccountIdentities(ImmutableArray<ExternalIdentity> all) => _all = all;

    public static AccountIdentities Of(ExternalIdentity first) => new([first]);

    /// <summary>A list read back from a store; an empty one, or one with an identity twice, is broken data.</summary>
    public static AccountIdentities Restore(IEnumerable<ExternalIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var all = identities.ToImmutableArray();
        if (all.IsEmpty)
        {
            throw new ArgumentException("An account holds at least one identity.", nameof(identities));
        }

        if (all.Distinct().Count() != all.Length)
        {
            throw new ArgumentException("An account holds an identity once.", nameof(identities));
        }

        return new AccountIdentities(all);
    }

    public IReadOnlyList<ExternalIdentity> All => _all;

    public bool Contains(ExternalIdentity identity) => _all.Contains(identity);

    /// <summary>The list with this identity added at the end; the same list when it already holds it.</summary>
    public AccountIdentities With(ExternalIdentity identity) => Contains(identity) ? this : new AccountIdentities(_all.Add(identity));

    /// <summary>The list without this identity: refused when it is not there, or when it is the only one.</summary>
    public Result<AccountIdentities> Without(ExternalIdentity identity)
    {
        if (!Contains(identity))
        {
            return Result.Failure<AccountIdentities>(AccountErrors.IdentityNotLinked);
        }

        return _all.Length == 1
            ? Result.Failure<AccountIdentities>(AccountErrors.LastIdentity)
            : Result.Success(new AccountIdentities(_all.Remove(identity)));
    }

    public bool Equals(AccountIdentities? other) => other is not null && _all.SequenceEqual(other._all);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var identity in _all)
        {
            hash.Add(identity);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(", ", _all);
}
