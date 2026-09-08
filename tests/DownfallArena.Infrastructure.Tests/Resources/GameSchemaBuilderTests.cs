using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

public sealed class GameSchemaBuilderTests
{
    [Fact]
    public void Valid_content_builds_a_sorted_schema_with_resolved_references_and_a_hash()
    {
        using var content = new ContentDirectory().WithValidContent();

        var schema = GameSchemaBuilder.Build(content.Path);

        schema.Spells.Select(spell => spell.Id).ShouldBe(["spell:guard:v1", "spell:strike:v1"]);
        schema.Creatures.Single().TalentTreeId.ShouldBe("talent-tree:base:v1");
        schema.Creatures.Single().StartingSpellIds.ShouldBe(["spell:strike:v1"]);
        schema.TalentTrees.Single().Root!.Spells.Single().Id.ShouldBe("spell:strike:v1");
        schema.TalentTrees.Single().Root!.Children.Single().Prerequisites!.AllOf.ShouldBe(["spell:strike:v1"]);
        schema.Aliases.Keys.ShouldBe(["spell:guard", "spell:strike", "talent-tree:base"]);
        schema.ContentHash.Length.ShouldBe(64);
        GameSchemaBuilder.ComputeHash(schema).ShouldBe(schema.ContentHash);
    }

    [Fact]
    public void The_hash_depends_only_on_content()
    {
        using var first = new ContentDirectory().WithValidContent();
        using var second = new ContentDirectory().WithValidContent();
        using var changed = new ContentDirectory().WithValidContent()
            .WithFile("Spells/strike.v1.json", """
                {
                  "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
                  "initiative": 1, "energyCost": 0, "criticalChance": 0,
                  "targeting": { "origin": "Enemy", "scope": "SingleTarget" },
                  "effects": [ { "kind": "Damage", "amount": 2 } ]
                }
                """);

        GameSchemaBuilder.Build(first.Path).ContentHash.ShouldBe(GameSchemaBuilder.Build(second.Path).ContentHash);
        GameSchemaBuilder.Build(changed.Path).ContentHash.ShouldNotBe(GameSchemaBuilder.Build(first.Path).ContentHash);
    }

    [Fact]
    public void Written_schemas_load_back_into_resources_stamped_with_the_hash()
    {
        using var content = new ContentDirectory().WithValidContent();
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, content.Output);
        var resources = GameSchemaBuilder.Load(Path.Combine(content.Output, GameSchemaJson.SchemaFileName));

        File.ReadAllText(Path.Combine(content.Output, GameSchemaJson.HashFileName)).ShouldBe(schema.ContentHash);
        resources.Version.ShouldBe(schema.ContentHash);
        var strike = resources.GetSpell(SpellId.Parse("spell:strike:v1"));
        strike.Effects.ShouldBe([Damage.Of(1), Bleed.Of(1, 2)]);
        var guard = resources.GetSpell(SpellId.Parse("spell:guard:v1"));
        guard.Effects.ShouldBe([DefenseBuff.Of(2, Duration.Permanent, StackingPolicy.Ignore)]);
        guard.Targeting.ShouldBe(TargetingSpec.SingleTarget(TargetOrigin.Self));
        resources.GetCreature(CreatureDefinitionId.Parse("creature:main:v1")).StartingSpells.ShouldBe([strike.Id]);
        resources.GetTalentTree(TalentTreeId.Parse("talent-tree:base:v1")).Spells.Count().ShouldBe(2);
    }

    [Fact]
    public void A_tampered_schema_is_rejected_on_load()
    {
        using var content = new ContentDirectory().WithValidContent();
        GameSchemaBuilder.Write(GameSchemaBuilder.Build(content.Path), content.Output);
        var schemaPath = Path.Combine(content.Output, GameSchemaJson.SchemaFileName);
        File.WriteAllText(schemaPath, File.ReadAllText(schemaPath).Replace("\"baseHealth\": 20", "\"baseHealth\": 99", StringComparison.Ordinal));

        var exception = Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(schemaPath));

        exception.Message.ShouldContain("content hash");
    }

    [Fact]
    public void Loading_a_missing_schema_says_to_run_the_builder()
    {
        Should.Throw<FileNotFoundException>(() => GameSchemaBuilder.Load(Path.Combine(AppContext.BaseDirectory, "nope.json")))
            .Message.ShouldContain("data builder");
    }

    [Fact]
    public void Unknown_aliases_and_malformed_ids_are_reported_with_their_context()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Creatures/main.v1.json", """
                {
                  "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature",
                  "baseHealth": 20, "baseEnergy": 0, "baseDefense": 0, "baseInitiative": 5, "baseCriticalChance": 0,
                  "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:nope", "creature:strike:v1"]
                }
                """);

        var exception = Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path));

        exception.Problems.ShouldContain(problem => problem.Contains("creature 'creature:main:v1'", StringComparison.Ordinal) && problem.Contains("'spell:nope'", StringComparison.Ordinal));
        exception.Problems.ShouldContain(problem => problem.Contains("'creature:strike:v1'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mistyped_field_is_reported_with_its_file()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/typo.v1.json", """
                {
                  "id": "spell:typo:v1", "name": "Typo", "spellType": "Offensive", "characterClass": "Creature",
                  "initiative": 1, "energyCost": 0, "criticalChance": 0,
                  "targeting": { "origin": "Enemy", "scope": "SingleTarget" },
                  "effects": [ { "kind": "Damage", "amount": 1 } ]
                }
                """);

        var exception = Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path));

        exception.Problems.ShouldHaveSingleItem().ShouldStartWith("typo.v1.json:");
        exception.Problems[0].ShouldContain("characterClass");
    }

    [Fact]
    public void Invalid_values_are_reported_together()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/broken.v1.json", """
                {
                  "id": "spell:broken:v1", "name": "Broken", "spellType": "Sideways", "creatureClass": "Creature",
                  "initiative": 1, "energyCost": -1, "criticalChance": 0,
                  "targeting": { "origin": "Enemy", "scope": "Multi", "maxTargets": 1 },
                  "effects": [
                    { "kind": "Damage" },
                    { "kind": "Stun", "permanent": true },
                    { "kind": "Teleport", "amount": 1 },
                    { "kind": "Bleed", "amountPerRound": 1, "durationRounds": 2, "stacking": "Sometimes" }
                  ]
                }
                """);

        var exception = Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path));

        var problems = string.Join("\n", exception.Problems);
        problems.ShouldContain("'Sideways' is not a valid 'spellType'");
        problems.ShouldContain("'amount' is required");
        problems.ShouldContain("cannot be permanent");
        problems.ShouldContain("unknown effect kind 'Teleport'");
        problems.ShouldContain("'Sometimes' is not a valid 'stacking'");
        problems.ShouldContain("at least two targets");
    }

    [Fact]
    public void A_missing_content_folder_is_reported()
    {
        using var content = new ContentDirectory().WithValidContent();
        Directory.Delete(Path.Combine(content.Path, GameSchemaBuilder.TalentTreesFolder), recursive: true);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Problems.ShouldContain("Content folder 'TalentTrees' is missing.");
        Should.Throw<DirectoryNotFoundException>(() => GameSchemaBuilder.Build(Path.Combine(content.Path, "elsewhere")));
    }

    [Fact]
    public void The_repository_content_builds_and_loads()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

        var schema = GameSchemaBuilder.Build(dataDirectory);
        var resources = GameSchemaMapper.ToGameResources(schema);

        resources.Version.ShouldBe(schema.ContentHash);
        resources.Creatures.ShouldNotBeEmpty();
        resources.Spells.Count.ShouldBeGreaterThan(30);
        resources.TalentTrees.ShouldHaveSingleItem();
    }
}
