using DA.Game.Shared.Contracts.Resources.Json;
using DA.Game.Shared.Utilities;
using System.Text.Json;

namespace DA.Game.Infrastructure.Bootstrap;

public static class GameResourcesFactory
{
    public static GameResources LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);

        var schema = JsonSerializer.Deserialize<GameSchema>(json, DownfallArenaJsonOptions.ReadOptions)
                     ?? throw new InvalidOperationException("Invalid schema file");

        Validate(schema);

        var aliasResolver = new SchemaAliasResolver(schema);

        var spells = schema.Spells.Select(s => s.ToRef(aliasResolver)).ToArray();
        var creatures = schema.Creatures.Select(c => c.ToRef(aliasResolver)).ToArray();
        var talentTrees = schema.TalentTrees.Select(t => t.ToRef(aliasResolver)).ToArray();

        var gameResources = GameResources.Create(spells, creatures, talentTrees, schema.BuildHash);
        return gameResources;
    }

    public static void Validate(GameSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var spellIds = schema.Spells.Select(s => s.Id).ToHashSet();
        foreach (var c in schema.Creatures)
        {
            foreach (var sid in c.StartingSpellIds)
                if (!spellIds.Contains(ResolveAlias(schema, sid)))
                    throw new InvalidOperationException($"Creature {c.Id} references missing spell {sid}");
        }
    }

    public static string ResolveAlias(GameSchema schema, string requestedIdOrBase)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(requestedIdOrBase);
        // Si déjà versionné → le renvoyer tel quel
        if (requestedIdOrBase.Contains(":v", StringComparison.InvariantCultureIgnoreCase)) return requestedIdOrBase;

        if (schema.Aliases is null || !schema.Aliases.TryGetValue(requestedIdOrBase, out var concrete))
            throw new KeyNotFoundException($"Alias not found: {requestedIdOrBase}");

        return concrete;
    }
}
