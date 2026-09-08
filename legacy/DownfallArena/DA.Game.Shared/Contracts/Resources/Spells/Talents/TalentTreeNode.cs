using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Talents;

// A node in the talent tree hierarchy (BaseCreature, Brawler, Warlord, etc.)
public sealed record TalentTreeNode(
    string Code,
    string Name,
    TalentPrerequisites Prerequisites,
    IReadOnlyList<TalentTreeSpellNode> Spells,
    IReadOnlyList<TalentTreeNode> Children) : ValueObject;
