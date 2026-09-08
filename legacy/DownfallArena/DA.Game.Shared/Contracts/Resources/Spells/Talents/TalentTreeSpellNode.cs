using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Talents;

// A spell entry inside a talent node
public sealed record TalentTreeSpellNode(
    SpellId SpellId,
    TalentPrerequisites Prerequisites) : ValueObject;
