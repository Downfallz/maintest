using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Contracts.Resources.Json;

public sealed class SpellDefinitionDto
{
    public string Id { get; set; } = default!;             // ex: "spell:fireball"
    public string Name { get; set; } = default!;
    public SpellType SpellType { get; set; } = default!;
    public CreatureClass CharacterClass { get; set; } = default!;
    public int Initiative { get; set; }
    public int EnergyCost { get; set; }
    public double CriticalChance { get; set; }             // ex: 0.15

    public IReadOnlyCollection<EffectDto> Effects { get; set; } = [];
}
