using DA.Game.Domain2.Catalog.Entities;
using DA.Game.Domain2.Catalog.Ids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Matches.ReadModels;
public sealed record CatalogSnapshot(
    string Version,            // ex: semver, timestamp ou hash
    DateTime LoadedAtUtc,
    IReadOnlyDictionary<CharacterDefId, CharacterDefinition> Characters,
    IReadOnlyDictionary<SpellId, SpellDefinition> Spells
);