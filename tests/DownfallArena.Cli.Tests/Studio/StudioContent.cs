namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// A scratch content directory with a small, valid content set, deleted when the test ends. The Cli tests keep
/// their own rather than reaching into the Infrastructure tests' one: the studio is a different consumer.
/// </summary>
internal sealed class StudioContent : IDisposable
{
    public const string Guard = """
        {
          "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
          "initiative": 2, "energyCost": 1, "criticalChance": 0,
          "targeting": { "origin": "Self", "scope": "SingleTarget" },
          "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
        }
        """;

    public StudioContent()
    {
        Path = System.IO.Path.Combine(AppContext.BaseDirectory, "scratch", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Path);
        Write("aliases.json", """
            { "spell:strike": "spell:strike:v1", "spell:guard": "spell:guard:v1", "talent-tree:base": "talent-tree:base:v1" }
            """);
        Write("Creatures/main.v1.json", """
            {
              "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature",
              "baseHealth": 20, "baseEnergy": 0, "baseDefense": 1, "baseInitiative": 5, "baseCriticalChance": 0.05,
              "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:strike"]
            }
            """);
        Write("Spells/strike.v1.json", """
            {
              "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
              "initiative": 1, "energyCost": 0, "criticalChance": 0,
              "targeting": { "origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1 },
              "effects": [ { "kind": "Damage", "amount": 1 } ]
            }
            """);
        Write("Spells/brawler/guard.v1.json", Guard);
        Write("TalentTrees/base.v1.json", """
            {
              "id": "talent-tree:base:v1", "name": "Base",
              "root": {
                "code": "Base", "name": "Base",
                "spells": [ { "id": "spell:strike" }, { "id": "spell:guard" } ]
              }
            }
            """);
    }

    public string Path { get; }

    public void Write(string relativePath, string json)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, json);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
