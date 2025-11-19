using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Resources.Spells;

namespace DA.Game.Domain2.Matches.ValueObjects;
public sealed record SpellUnlockChoice(CharacterId CharacterId, SpellRef SpellRef) : ValueObject()
{
    public static SpellUnlockChoice Create(CharacterId id, SpellRef spellRef)
    {
        return new SpellUnlockChoice(id, spellRef);
    }
}