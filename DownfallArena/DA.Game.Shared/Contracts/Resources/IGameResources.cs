using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Shared.Contracts.Resources;

public interface IGameResources {
    IReadOnlyList<CharacterDefinitionRef> Characters { get; }
    IReadOnlyList<Spell> Spells { get; }
    string Version { get; }
    CharacterDefinitionRef GetCharacter(CharacterDefId id);
    Spell GetSpell(SpellId id);
    bool TryGetCharacter(CharacterDefId id, out CharacterDefinitionRef? def);
    bool TryGetSpell(SpellId id, out Spell? spell);
}