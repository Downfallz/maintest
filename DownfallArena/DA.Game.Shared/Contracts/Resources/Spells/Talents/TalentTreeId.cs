using DA.Game.Shared.Utilities;

namespace DA.Game.Shared.Contracts.Resources.Spells.Talents;

// Simple value object for the talent tree id (matches JSON "id")
public readonly record struct TalentTreeId(string Value)
{
    public override string ToString() => Value;
}
