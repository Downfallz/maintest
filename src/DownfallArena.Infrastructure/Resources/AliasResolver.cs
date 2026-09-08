using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Resources;

/// <summary>
/// Turns a reference written by a content author (versioned id or alias) into a versioned id.
/// </summary>
public sealed class AliasResolver
{
    private readonly IReadOnlyDictionary<string, string> _aliases;

    public AliasResolver(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        _aliases = aliases;
    }

    /// <summary>
    /// Returns the versioned id for <paramref name="reference"/>, or <c>null</c> after recording the problem.
    /// </summary>
    public string? Resolve<TId>(string reference, string context, ICollection<string> problems)
        where TId : VersionedId, IVersionedId<TId>
    {
        ArgumentNullException.ThrowIfNull(problems);

        if (VersionedId.TryParse<TId>(reference, out _))
        {
            return reference;
        }

        if (!_aliases.TryGetValue(reference, out var target))
        {
            problems.Add($"{context}: '{reference}' is neither a versioned {TId.Kind} id nor a known alias.");
            return null;
        }

        if (!VersionedId.TryParse<TId>(target, out _))
        {
            problems.Add($"{context}: alias '{reference}' points to '{target}', which is not a versioned {TId.Kind} id.");
            return null;
        }

        return target;
    }
}
