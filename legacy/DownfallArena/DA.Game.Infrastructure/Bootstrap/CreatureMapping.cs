using DA.Game.Shared.Contracts.Resources.Creatures;
using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Talents;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Infrastructure.Bootstrap;

public static class CreatureMapping
{
    public static CreatureDefinitionRef ToRef(
        this CreatureDefinitionDto dto,
        IIdAliasResolver aliasResolver)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(aliasResolver);

        var creatureId = new CreatureDefId(dto.Id);

        // Talent tree can be null / empty in JSON (e.g. bosses, simple monsters)
        TalentTreeId? talentTreeId = null;
        if (!string.IsNullOrWhiteSpace(dto.TalentTreeId))
        {
            var canonicalTalentTreeId = aliasResolver.Resolve(dto.TalentTreeId);
            talentTreeId = new TalentTreeId(canonicalTalentTreeId);
        }

        var startingSpells = dto.StartingSpellIds
            .Select(aliasResolver.Resolve)
            .Select(SpellId.New)
            .ToList();

        return CreatureDefinitionRef.Create(
            creatureId,
            dto.Name,
            Health.Of(dto.BaseHealth),
            Energy.Of(dto.BaseEnergy),
            Defense.Of(dto.BaseDefense),
            Initiative.Of(dto.BaseInitiative),
            CriticalChance.Of(dto.BaseCriticalChance),
            talentTreeId,
            startingSpells
        );
    }
}
