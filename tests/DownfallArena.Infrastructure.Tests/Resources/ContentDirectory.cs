namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// A scratch content directory under the test output folder, deleted when the test ends.
/// </summary>
internal sealed class ContentDirectory : IDisposable
{
    public ContentDirectory()
    {
        Path = System.IO.Path.Combine(AppContext.BaseDirectory, "scratch", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Output => System.IO.Path.Combine(Path, "dst");

    public ContentDirectory WithFile(string relativePath, string json)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>
    /// A minimal, valid content set: one creature, two spells, one tree, aliases.
    /// </summary>
    public ContentDirectory WithValidContent() =>
        WithFile("aliases.json", """
            { "spell:strike": "spell:strike:v1", "spell:guard": "spell:guard:v1", "talent-tree:base": "talent-tree:base:v1" }
            """)
        .WithFile("Creatures/main.v1.json", """
            {
              "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature",
              "baseHealth": 20, "baseEnergy": 0, "baseDefense": 1, "baseInitiative": 5, "baseCriticalChance": 0.05,
              "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:strike"]
            }
            """)
        .WithFile("Spells/strike.v1.json", """
            {
              "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
              "initiative": 1, "energyCost": 0, "criticalChance": 0,
              "targeting": { "origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1 },
              "effects": [ { "kind": "Damage", "amount": 1 }, { "kind": "Bleed", "amountPerRound": 1, "durationRounds": 2 } ]
            }
            """)
        .WithFile("Spells/brawler/guard.v1.json", """
            {
              "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
              "initiative": 2, "energyCost": 1, "criticalChance": 0,
              "targeting": { "origin": "Self", "scope": "SingleTarget" },
              "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
            }
            """)
        .WithFile("TalentTrees/base.v1.json", """
            {
              "id": "talent-tree:base:v1", "name": "Base",
              "root": {
                "code": "Base", "name": "Base",
                "spells": [ { "id": "spell:strike" } ],
                "children": [
                  { "code": "Brawler", "name": "Brawler", "prerequisites": { "allOf": ["spell:strike"], "anyOf": [] },
                    "spells": [ { "id": "spell:guard", "prerequisites": { "allOf": [], "anyOf": ["spell:strike"] } } ] }
                ]
              }
            }
            """);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
