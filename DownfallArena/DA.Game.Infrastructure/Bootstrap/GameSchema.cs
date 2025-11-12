using DA.Game.Domain2.Catalog.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Infrastructure.Bootstrap;
public sealed record GameSchema
{
    public int SchemaVersion { get; init; } = 1;
    public required List<SpellDefinition> Spells { get; init; }
    public required List<CharacterDefinition> Creatures { get; init; }

    // Optionnel : pour pointer la version active de chaque item
    public Dictionary<string, string>? Aliases { get; init; }

    // Optionnel : hash ou nom de build
    public string? BuildHash { get; init; }
}
