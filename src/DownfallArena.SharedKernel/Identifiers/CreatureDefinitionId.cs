using System.Diagnostics.CodeAnalysis;

namespace DownfallArena.SharedKernel.Identifiers;

/// <summary>
/// Identity of a creature definition in the game resources: <c>creature:name:vN</c>.
/// </summary>
public sealed record CreatureDefinitionId : VersionedId, IVersionedId<CreatureDefinitionId>
{
    private CreatureDefinitionId(string value, string name, int version)
        : base(value, name, version)
    {
    }

    public static string Kind => "creature";

    public static CreatureDefinitionId Parse(string text) => Parse<CreatureDefinitionId>(text);

    public static bool TryParse(string? text, [NotNullWhen(true)] out CreatureDefinitionId? id) => TryParse<CreatureDefinitionId>(text, out id);

    static CreatureDefinitionId IVersionedId<CreatureDefinitionId>.Create(string value, string name, int version) => new(value, name, version);
}
