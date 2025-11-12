
using DA.Game.Domain2.Catalog.Ids;
using DA.Game.Domain2.Catalog.ValueObjects;
using DA.Game.Domain2.Catalog.ValueObjects.Character;
using DA.Game.Domain2.Catalog.ValueObjects.Spells.Effects.Base;
using DA.Game.Domain2.Catalog.ValueObjects.Spells.Enum;
using DA.Game.Domain2.Shared.Primitives;
using System.Collections.Immutable;

namespace DA.Game.Infrastructure.Catalog.DTOs;

public sealed class SpellDefinitionDto
{
    public string Id { get; set; } = default!;             // ex: "fireball"
    public string Name { get; set; } = default!;
    public SpellType SpellType { get; set; }
    public CharClass CharacterClass { get; set; }
    public int Initiative { get; set; }
    public int Level { get; set; }

    public int? EnergyCost { get; set; }
    public double? CriticalChance { get; set; }             // ex: 0.15

    public List<EffectDto> Effects { get; set; } = [];
}

public sealed class EffectDto
{
    public string EffectType { get; set; } = default!;      // ex: "Direct", "Temporary"
    public string AffectedStat { get; set; } = default!;    // ex: "Health"
    public int Modifier { get; set; }                       // ex: -10
    public int? Duration { get; set; }                      // nullable pour Direct
}
