using DA.Game.Domain2.Matches.Resources;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Match.ValueObjects;
public sealed record SpellUnlockChoice(CharacterId CharacterId, SpellRef SpellRef) : ValueObject()
{
    public static SpellUnlockChoice Create(CharacterId id, SpellRef spellRef)
    {
        return new SpellUnlockChoice(id, spellRef);
    }
}