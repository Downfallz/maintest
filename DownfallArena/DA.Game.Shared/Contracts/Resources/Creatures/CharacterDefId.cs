using System.Text.RegularExpressions;

namespace DA.Game.Shared.Contracts.Resources.Creatures;

public readonly record struct CharacterDefId(string Value)
{
    private static readonly Regex Pattern =
        new(@"^[a-zA-Z0-9_\-]+:[a-zA-Z0-9_\-]+:v[0-9]+$", RegexOptions.Compiled);

    public static CharacterDefId New(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("CharacterDefId cannot be empty.", nameof(id));

        if (!Pattern.IsMatch(id))
            throw new FormatException($"Invalid CharacterDefId format: '{id}'. Expected '<category>:<name>:v<number>'.");

        return new CharacterDefId(id);
    }

    public override string ToString() => Value;

    // Optionnel : extraire facilement les parties
    public (string Category, string Name, int Version) Parse()
    {
        var parts = Value.Split(':');
        var version = int.Parse(parts[2].Substring(1)); // remove 'v'
        return (parts[0], parts[1], version);
    }
}
