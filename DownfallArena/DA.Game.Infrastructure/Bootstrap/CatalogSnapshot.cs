using DA.Game.Domain2.Catalog.Entities;
using DA.Game.Domain2.Catalog.Ids;

namespace DA.Game.Infrastructure.Bootstrap;

public sealed record CatalogSnapshot(
    string Version,            // ex: semver, timestamp ou hash
    DateTime LoadedAtUtc,
    IReadOnlyDictionary<CharacterDefId, CharacterDefinition> Characters,
    IReadOnlyDictionary<SpellId, SpellDefinition> Spells
);