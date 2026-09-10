using DownfallArena.Domain.Resources;
using DownfallArena.Domain.Resources.Effects;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

public sealed class GameSchemaBuilderTests
{
    private const string DisabledGuard = """
        {
          "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
          "enabled": false,
          "initiative": 2, "energyCost": 1, "criticalChance": 0,
          "targeting": { "origin": "Self", "scope": "SingleTarget" },
          "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
        }
        """;

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
    public void A_disabled_spell_leaves_the_build_and_every_reference_to_it()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", DisabledGuard)
            .WithFile("Creatures/main.v1.json", """
                {
                  "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature",
                  "baseHealth": 20, "baseEnergy": 0, "baseDefense": 1, "baseInitiative": 5, "baseCriticalChance": 0.05,
                  "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:strike", "spell:guard"]
                }
                """)
            .WithFile("TalentTrees/base.v1.json", """
                {
                  "id": "talent-tree:base:v1", "name": "Base",
                  "root": {
                    "code": "Base", "name": "Base",
                    "spells": [ { "id": "spell:strike" } ],
                    "children": [
                      { "code": "Brawler", "name": "Brawler", "prerequisites": { "allOf": ["spell:strike", "spell:guard"], "anyOf": [] },
                        "spells": [ { "id": "spell:guard", "prerequisites": { "allOf": ["spell:guard"], "anyOf": [] } } ] }
                    ]
                  }
                }
                """);
        var notes = new List<string>();

        var schema = GameSchemaBuilder.Build(content.Path, notes);

        var brawler = schema.TalentTrees.Single().Root!.Children.Single();
        schema.Spells.Select(spell => spell.Id).ShouldBe(["spell:strike:v1"]);
        schema.Creatures.Single().StartingSpellIds.ShouldBe(["spell:strike:v1"]);
        brawler.Spells.ShouldBeEmpty();
        brawler.Prerequisites!.AllOf.ShouldBe(["spell:strike:v1"]);
        var note = string.Join("\n", notes);
        note.ShouldContain("Disabled spell 'spell:guard:v1' is not in the build.");
        note.ShouldContain("node 'Brawler': dropped disabled spell 'spell:guard:v1'.");
        note.ShouldContain("creature 'creature:main:v1' starting spells: dropped disabled spell 'spell:guard:v1'.");
        note.ShouldContain("node 'Brawler' prerequisites: dropped disabled spell 'spell:guard:v1'.");
    }

    [Fact]
    public void A_disabled_creature_leaves_the_build()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Creatures/main.v1.json", """
                {
                  "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature", "enabled": false,
                  "baseHealth": 20, "baseEnergy": 0, "baseDefense": 1, "baseInitiative": 5, "baseCriticalChance": 0.05,
                  "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:strike"]
                }
                """);
        var notes = new List<string>();

        GameSchemaBuilder.Build(content.Path, notes).Creatures.ShouldBeEmpty();

        notes.ShouldContain("Disabled creature 'creature:main:v1' is not in the build.");
    }

    [Fact]
    public void A_creature_whose_talent_tree_is_disabled_is_a_problem()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("TalentTrees/base.v1.json", """
                {
                  "id": "talent-tree:base:v1", "name": "Base", "enabled": false,
                  "root": { "code": "Base", "name": "Base", "spells": [ { "id": "spell:strike" } ] }
                }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Problems.ShouldContain(problem => problem.Contains("talent tree 'talent-tree:base:v1' is disabled", StringComparison.Ordinal));
    }

    [Fact]
    public void A_creature_left_without_a_starting_spell_by_a_disable_says_so()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/strike.v1.json", """
                {
                  "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
                  "enabled": false,
                  "initiative": 1, "energyCost": 0, "criticalChance": 0,
                  "targeting": { "origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1 },
                  "effects": [ { "kind": "Damage", "amount": 1 } ]
                }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Problems.ShouldContain(problem => problem.Contains("every starting spell is disabled", StringComparison.Ordinal));
    }

    /// <summary>
    /// The switch reaches a node's own gate, a spell's gate inside a node, and every depth of the tree, not just
    /// the spell list of the node it is written next to. Every gate here keeps a spell, so every one is pruned
    /// rather than refused.
    /// </summary>
    [Fact]
    public void A_disable_reaches_every_prerequisite_at_every_depth()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", DisabledGuard)
            .WithFile("TalentTrees/base.v1.json", """
                {
                  "id": "talent-tree:base:v1", "name": "Base",
                  "root": {
                    "code": "Base", "name": "Base",
                    "spells": [ { "id": "spell:strike" } ],
                    "children": [
                      {
                        "code": "Brawler", "name": "Brawler",
                        "prerequisites": { "allOf": ["spell:strike", "spell:guard"], "anyOf": [] },
                        "spells": [ { "id": "spell:strike", "prerequisites": { "allOf": ["spell:strike", "spell:guard"], "anyOf": [] } } ],
                        "children": [
                          {
                            "code": "Deep", "name": "Deep",
                            "prerequisites": { "allOf": ["spell:strike", "spell:guard"], "anyOf": ["spell:strike", "spell:guard"] },
                            "spells": []
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

        var root = GameSchemaBuilder.Build(content.Path).TalentTrees.Single().Root!;

        var brawler = root.Children.Single();
        brawler.Prerequisites!.AllOf.ShouldBe(["spell:strike:v1"]);
        brawler.Spells.Single().Prerequisites!.AllOf.ShouldBe(["spell:strike:v1"]);
        var deep = brawler.Children.Single();
        deep.Prerequisites!.AllOf.ShouldBe(["spell:strike:v1"]);
        deep.Prerequisites.AnyOf.ShouldBe(["spell:strike:v1"]);
    }

    /// <summary>
    /// An empty gate is no requirement at all: <c>TalentPrerequisites</c> reads both an empty <c>allOf</c> and an
    /// empty <c>anyOf</c> as satisfied. Pruning the last spell out of either would unlock what it gates rather
    /// than close it, so the builder refuses instead of quietly opening a talent branch.
    /// </summary>
    [Theory]
    [InlineData("allOf")]
    [InlineData("anyOf")]
    public void Disabling_every_spell_of_a_gate_is_refused_rather_than_opening_the_node(string gate)
    {
        var other = gate == "allOf" ? "anyOf" : "allOf";
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", DisabledGuard)
            .WithFile("TalentTrees/base.v1.json", $$"""
                {
                  "id": "talent-tree:base:v1", "name": "Base",
                  "root": {
                    "code": "Base", "name": "Base",
                    "spells": [ { "id": "spell:strike" } ],
                    "children": [
                      {
                        "code": "Brawler", "name": "Brawler",
                        "prerequisites": { "{{gate}}": ["spell:guard"], "{{other}}": [] },
                        "spells": []
                      }
                    ]
                  }
                }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Problems.ShouldContain(problem => problem.Contains($"every spell of its '{gate}' is disabled", StringComparison.Ordinal));
    }

    /// <summary>The gate of a spell inside a node is guarded the same way as the gate of the node itself.</summary>
    [Fact]
    public void Disabling_every_spell_of_a_gate_on_a_talent_spell_is_refused_too()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", DisabledGuard)
            .WithFile("TalentTrees/base.v1.json", """
                {
                  "id": "talent-tree:base:v1", "name": "Base",
                  "root": {
                    "code": "Base", "name": "Base",
                    "spells": [ { "id": "spell:strike", "prerequisites": { "allOf": ["spell:guard"], "anyOf": [] } } ]
                  }
                }
                """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Build(content.Path))
            .Problems.ShouldContain(problem => problem.Contains("spell 'spell:strike:v1': every spell of its 'allOf' is disabled", StringComparison.Ordinal));
    }

    /// <summary>A node with no prerequisites block at all keeps none, rather than gaining an empty one.</summary>
    [Fact]
    public void A_node_without_prerequisites_stays_without_them()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", DisabledGuard);

        var root = GameSchemaBuilder.Build(content.Path).TalentTrees.Single().Root!;

        root.Prerequisites.ShouldBeNull();
        root.Spells.Single().Prerequisites.ShouldBeNull();
    }

    [Fact]
    public void Saying_an_item_is_enabled_does_not_move_the_content_hash()
    {
        using var untouched = new ContentDirectory().WithValidContent();
        using var explicitly = new ContentDirectory().WithValidContent()
            .WithFile("Spells/strike.v1.json", """
                {
                  "id": "spell:strike:v1", "name": "Strike", "spellType": "Offensive", "creatureClass": "Creature",
                  "enabled": true,
                  "initiative": 1, "energyCost": 0, "criticalChance": 0,
                  "targeting": { "origin": "Enemy", "scope": "SingleTarget", "maxTargets": 1 },
                  "effects": [ { "kind": "Damage", "amount": 1 }, { "kind": "Bleed", "amountPerRound": 1, "durationRounds": 2 } ]
                }
                """);

        GameSchemaBuilder.Build(explicitly.Path).ContentHash.ShouldBe(GameSchemaBuilder.Build(untouched.Path).ContentHash);
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

    /// <summary>
    /// The authored `Regeneration` kind reaches the domain as the effect (ADR 0019). Healing Screech is the
    /// content that uses it, so this is the mapping and the content in one assertion.
    /// </summary>
    [Fact]
    public void A_regeneration_is_authored_by_its_kind_and_maps_to_the_effect()
    {
        var resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(Path.Combine(AppContext.BaseDirectory, "data")));

        var screech = resources.GetSpell(SpellId.Parse("spell:healing_screech:v1"));

        screech.Effects.OfType<Regeneration>().ShouldHaveSingleItem()
            .ShouldBe(Regeneration.Of(2, rounds: 1));
    }

    /// <summary>
    /// ADR 0020: the authored `Attunement` kind reaches the domain as the effect, in the same per-round family
    /// as Bleed and Regeneration. No repository content uses it yet, so the content is authored here.
    /// </summary>
    [Fact]
    public void An_attunement_is_authored_by_its_kind_and_maps_to_the_effect()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", """
                {
                  "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
                  "initiative": 2, "energyCost": 1, "criticalChance": 0,
                  "targeting": { "origin": "Self", "scope": "SingleTarget" },
                  "effects": [ { "kind": "Attunement", "amountPerRound": 2, "durationRounds": 3 } ]
                }
                """);

        var resources = GameSchemaMapper.ToGameResources(GameSchemaBuilder.Build(content.Path));

        resources.GetSpell(SpellId.Parse("spell:guard:v1")).Effects.ShouldBe([Attunement.Of(2, rounds: 3)]);
    }
}
