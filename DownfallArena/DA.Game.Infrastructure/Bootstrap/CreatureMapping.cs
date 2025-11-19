using DA.Game.Shared.Contracts.Catalog.Ids;
using DA.Game.Shared.Resources.Creatures;
using DA.Game.Shared.Resources.JsonDto;
namespace DA.Game.Infrastructure.Bootstrap;

public static class CreatureMapping
{
    //public static Result<CharacterDefinition> ToDomain(this CreatureDefinitionDto dto, GameSchema schema)
    //{
    //    // validations légères côté DTO
    //    if (string.IsNullOrWhiteSpace(dto.Name))
    //        return Result<CharacterDefinition>.Fail("Name is required.");
    //    if (dto.StartingSpellIds is null || dto.StartingSpellIds.Length == 0)
    //        return Result<CharacterDefinition>.Fail("At least one starting spell is required.");

    //    // construction domaine (VO)
    //    var id = new CharacterDefId(dto.Id);
    //    var health = Health.Of(dto.BaseHealth);
    //    var energy = Energy.Of(dto.BaseEnergy);
    //    var defense = Defense.Of(dto.BaseDefense);
    //    var init = Initiative.Of(dto.BaseInitiative);
    //    var crit = CriticalChance.Of(Percentage01.Of(dto.BaseCriticalChance));

    //    var spellIds = dto.StartingSpellIds.Select(g => new SpellId(GameResourcesFactory.ResolveAlias(schema, g)));
    //    var charDef = CharacterDefinition.Create(id,
    //                dto.Name,
    //                dto.CharacterClass,
    //                health,
    //                energy,
    //                defense,
    //                init,
    //                crit, spellIds);

    //    return Result<CharacterDefinition>.Ok(charDef);

    //}

    public static CharacterDefinitionRef ToRef(this CreatureDefinitionDto dto)
        => new CharacterDefinitionRef(
            new CharacterDefId(dto.Id),
            dto.Name,
            dto.BaseHealth,
            dto.BaseEnergy,
            dto.BaseDefense,
            dto.BaseInitiative,
            dto.BaseCriticalChance,
            dto.StartingSpellIds.Select(sid => new SpellId(sid)).ToList()
        );
}
