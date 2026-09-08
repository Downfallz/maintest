using System.Text.RegularExpressions;

namespace DA.Game.Shared.Contracts.Resources.Spells;

public readonly record struct SpellId(string Name)
{
    private static readonly Regex Pattern =
        new(@"^spell:[a-zA-Z0-9_\-]+:v[0-9]+$", RegexOptions.Compiled);

    public static SpellId New(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("SpellId cannot be empty.", nameof(name));

        if (!Pattern.IsMatch(name))
            throw new FormatException(
                $"Invalid SpellId format: '{name}'. Expected 'spell:<name>:v<number>'."
            );

        return new SpellId(name);
    }

    public override string ToString() => Name;

    public (string Category, string SpellName, int Version) Parse()
    {
        var parts = Name.Split(':');
        var version = int.Parse(parts[2].Substring(1));
        return (parts[0], parts[1], version);
    }
}
