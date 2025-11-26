using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Spells;

namespace DA.Game.Shared.Contracts.Resources;

public interface IGameResources
{
    IReadOnlyList<CreatureDefinitionRef> Creatures { get; }
    IReadOnlyList<Spell> Spells { get; }
    string Version { get; }
    CreatureDefinitionRef GetCreature(CreatureDefId id);
    Spell GetSpell(SpellId id);
    bool TryGetCreature(CreatureDefId id, out CreatureDefinitionRef? def);
    bool TryGetSpell(SpellId id, out Spell? spell);
}