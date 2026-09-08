using System.Diagnostics.CodeAnalysis;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a spell in the game resources: <c>spell:name:vN</c>.
/// </summary>
public sealed record SpellId : VersionedId, IVersionedId<SpellId>
{
    private SpellId(string value, string name, int version)
        : base(value, name, version)
    {
    }

    public static string Kind => "spell";

    public static SpellId Parse(string text) => Parse<SpellId>(text);

    public static bool TryParse(string? text, [NotNullWhen(true)] out SpellId? id) => TryParse<SpellId>(text, out id);

    static SpellId IVersionedId<SpellId>.Create(string value, string name, int version) => new(value, name, version);
}
