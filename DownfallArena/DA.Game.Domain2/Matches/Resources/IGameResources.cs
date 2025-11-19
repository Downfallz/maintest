using DA.Game.Domain2.Catalog.Ids;

namespace DA.Game.Domain2.Matches.Resources
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