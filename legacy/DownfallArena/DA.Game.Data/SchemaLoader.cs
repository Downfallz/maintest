using System.Text.Json;

namespace DA.Game.Data;

public static class SchemaLoader
{
    public static GameSchema LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var schema = JsonSerializer.Deserialize<GameSchema>(json, options)
                     ?? throw new InvalidOperationException("Invalid schema file");
        Validate(schema);
        return schema;
    }

    public static void Validate(GameSchema schema)
    {
        var spellIds = schema.Spells.Select(s => s.Id).ToHashSet();
        foreach (var c in schema.Creatures)
        {
            foreach (var sid in c.StartingSpellIds)
                if (!spellIds.Contains(sid))
                    throw new InvalidOperationException($"Creature {c.Id} references missing spell {sid}");
        }
    }

    public static IReadOnlyDictionary<string, SpellDef> BuildSpellIndex(GameSchema schema)
        => schema.Spells.ToDictionary(s => s.Id);

    public static string ResolveAlias(GameSchema schema, string requestedIdOrBase)
    {
        // Si déjà versionné → le renvoyer tel quel
        if (requestedIdOrBase.Contains(":v")) return requestedIdOrBase;

        if (schema.Aliases is null || !schema.Aliases.TryGetValue(requestedIdOrBase, out var concrete))
            throw new KeyNotFoundException($"Alias not found: {requestedIdOrBase}");

        return concrete;
    }
}
