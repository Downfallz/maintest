using DA.Game.Shared.Contracts.Catalog.Enums;

namespace DA.Game.Shared.Resources.JsonDto;

public sealed class SpellDefinitionDto
{
    public string Id { get; set; } = default!;             // ex: "spell:fireball"
    public string Name { get; set; } = default!;
    public SpellType SpellType { get; set; } = default!;
    public CharClass CharacterClass { get; set; } = default!;
    public int Initiative { get; set; }
    public int EnergyCost { get; set; }
    public double CriticalChance { get; set; }             // ex: 0.15

    public List<EffectDto> Effects { get; set; } = [];
}
