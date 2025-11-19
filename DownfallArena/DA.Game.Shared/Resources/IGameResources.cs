using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Resources.Creatures;
using DA.Game.Shared.Resources.Spells;

namespace DA.Game.Shared.Resources
{
    public interface IGameResources
    {
        IReadOnlyList<CharacterDefinitionRef> Characters { get; }
        IReadOnlyList<SpellRef> Spells { get; }
        string Version { get; }
        CharacterDefinitionRef GetCharacter(CharacterDefId id);
        SpellRef GetSpell(SpellId id);
        bool TryGetCharacter(CharacterDefId id, out CharacterDefinitionRef? def);
        bool TryGetSpell(SpellId id, out SpellRef? spell);
    }
}