using System.Diagnostics.CodeAnalysis;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a talent tree in the game resources: <c>talent-tree:name:vN</c>.
/// </summary>
public sealed record TalentTreeId : VersionedId, IVersionedId<TalentTreeId>
{
    private TalentTreeId(string value, string name, int version)
        : base(value, name, version)
    {
    }

    public static string Kind => "talent-tree";

    public static TalentTreeId Parse(string text) => Parse<TalentTreeId>(text);

    public static bool TryParse(string? text, [NotNullWhen(true)] out TalentTreeId? id) => TryParse<TalentTreeId>(text, out id);

    static TalentTreeId IVersionedId<TalentTreeId>.Create(string value, string name, int version) => new(value, name, version);
}
