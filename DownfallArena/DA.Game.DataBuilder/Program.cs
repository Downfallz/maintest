using DA.Game.Data;
using DA.Game.Infrastructure.Bootstrap;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DA.Game.DataBuilder;

internal class Program
{
    static async Task Main(string[] args)
    {

        string src = Path.Combine(AppContext.BaseDirectory, "../../../../../data");
        string dst = Path.Combine(AppContext.BaseDirectory, "../../../../../dist");
        Directory.CreateDirectory(dst);

        // Charge tous les spells/creatures (chaque fichier est un objet complet)
        static IEnumerable<T> LoadAll<T>(string dir)
        {
            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                yield return JsonSerializer.Deserialize<T>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        var spells = LoadAll<SpellDefinition>(Path.Combine(src, "spells")).ToList();
        var creatures = LoadAll<CreatureDefinition>(Path.Combine(src, "creatures")).ToList();

        // Aliases facultatifs
        Dictionary<string, string>? aliases = null;
        var aliasPath = Path.Combine(src, "aliases.json");
        if (File.Exists(aliasPath))
            aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(aliasPath));

        // Construit le bundle
        var schema = new GameSchema
        {
            SchemaVersion = 1,
            Spells = spells,
            Creatures = creatures,
            Aliases = aliases
        };

        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        schema = schema with { BuildHash = hash }; // réinjecte le hash
        json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(Path.Combine(dst, "game.schema.json"), json);
        File.WriteAllText(Path.Combine(dst, "schema.hash"), hash);

        Console.WriteLine($"✅ Built game.schema.json ({hash})");

    }

}


