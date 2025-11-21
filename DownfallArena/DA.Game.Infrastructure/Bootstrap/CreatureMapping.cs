using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Infrastructure.Bootstrap;

public static class CreatureMapping
{
    public static CharacterDefinitionRef ToRef(this CreatureDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new CharacterDefinitionRef(
            new CharacterDefId(dto.Id),
            dto.Name,
            Health.Of(dto.BaseHealth),
            Energy.Of(dto.BaseEnergy),
            Defense.Of(dto.BaseDefense),
            Initiative.Of(dto.BaseInitiative),
            CriticalChance.Of(Percentage01.Of(dto.BaseCriticalChance)),
            dto.StartingSpellIds.Select(id => new SpellId(id)).ToList()
        );
    }
}
