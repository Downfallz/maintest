using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record SpellUnlockChoice(CharacterId CharacterId, Spell SpellRef) : ValueObject()
{
    public static SpellUnlockChoice Create(CharacterId id, Spell spellRef)
    {
        return new SpellUnlockChoice(id, spellRef);
    }
}