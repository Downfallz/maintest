using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a content item in the game resources, in the form <c>kind:name:vN</c> (e.g. <c>spell:pummel:v1</c>).
/// Concrete identifiers fix the <c>kind</c> segment so a spell id can never be mistaken for a creature id.
/// </summary>
public abstract partial record VersionedId
{
    protected VersionedId(string value, string name, int version)
    {
        Value = value;
        Name = name;
        Version = version;
    }

    public string Value { get; }

    public string Name { get; }

    public int Version { get; }

    public sealed override string ToString() => Value;

    public static TSelf Parse<TSelf>(string text)
        where TSelf : VersionedId, IVersionedId<TSelf>
    {
        return TryParse<TSelf>(text, out var id)
            ? id
            : throw new FormatException($"'{text}' is not a valid {typeof(TSelf).Name}. Expected '{TSelf.Kind}:<name>:v<number>'.");
    }

    public static bool TryParse<TSelf>(string? text, [NotNullWhen(true)] out TSelf? id)
        where TSelf : VersionedId, IVersionedId<TSelf>
    {
        id = null;

        if (text is null)
        {
            return false;
        }

        var match = Pattern().Match(text);
        if (!match.Success || !string.Equals(match.Groups["kind"].Value, TSelf.Kind, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(match.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return false;
        }

        id = TSelf.Create(text, match.Groups["name"].Value, version);
        return true;
    }

    [GeneratedRegex("^(?<kind>[A-Za-z0-9_-]+):(?<name>[A-Za-z0-9_-]+):v(?<version>[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
