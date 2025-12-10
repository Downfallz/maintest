using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Talents;

// Prerequisites for unlocking a node or a spell
public sealed record TalentPrerequisites(
    IReadOnlyList<SpellId> AllOf,
    IReadOnlyList<SpellId> AnyOf) : ValueObject
{
    public static TalentPrerequisites Empty { get; } =
        new(Array.Empty<SpellId>(), Array.Empty<SpellId>());
}
