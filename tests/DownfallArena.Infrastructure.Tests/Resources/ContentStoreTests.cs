using System.Text.Json;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Resources.Authoring;

namespace DownfallArena.Infrastructure.Tests.Resources;

public sealed class ContentStoreTests
{
    private const string Guard = """
        {
          "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive", "creatureClass": "Brawler",
          "initiative": 2, "energyCost": 1, "criticalChance": 0,
          "targeting": { "origin": "Self", "scope": "SingleTarget" },
          "effects": [ { "kind": "DefenseBuff", "amount": 2, "permanent": true, "stacking": "Ignore" } ]
        }
        """;

    [Fact]
    public void Reading_lists_every_authored_file_with_what_it_says_about_itself()
    {
        using var content = new ContentDirectory().WithValidContent();

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Creatures.ShouldHaveSingleItem().Id.ShouldBe("creature:main:v1");
        catalogue.Spells.Select(spell => spell.Path).ShouldBe(["Spells/brawler/guard.v1.json", "Spells/strike.v1.json"]);
        catalogue.Spells.ShouldAllBe(spell => spell.Enabled);
        catalogue.TalentTrees.ShouldHaveSingleItem().Name.ShouldBe("Base");
        catalogue.Aliases.ShouldContainKey("spell:strike");
        catalogue.ContentHash.ShouldNotBeNull().Length.ShouldBe(64);
        catalogue.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void Reading_content_that_does_not_build_still_lists_it_and_says_why()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Creatures/main.v1.json", """
                {
                  "id": "creature:main:v1", "name": "Main", "creatureClass": "Creature",
                  "baseHealth": 20, "baseEnergy": 0, "baseDefense": 1, "baseInitiative": 5, "baseCriticalChance": 0.05,
                  "talentTreeId": "talent-tree:missing:v1", "startingSpellIds": ["spell:strike"]
                }
                """);

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Spells.Count.ShouldBe(2);
        catalogue.ContentHash.ShouldBeNull();
        catalogue.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_file_that_does_not_parse_is_listed_with_its_problem()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("Spells/torn.v1.json", "{ not json");

        var torn = new ContentStore(content.Path).Read().Spells.Single(spell => spell.Path == "Spells/torn.v1.json");

        torn.Problem.ShouldNotBeNullOrWhiteSpace();
        torn.Id.ShouldBe("Spells/torn.v1.json");
    }

    [Fact]
    public void Saving_writes_the_document_and_the_next_read_sees_it()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        var path = store.Save(ContentKind.Spell, "Spells/brawler/guard.v2.json", Guard.Replace("spell:guard:v1", "spell:guard:v2", StringComparison.Ordinal));

        path.ShouldBe("Spells/brawler/guard.v2.json");
        var saved = store.Read().Spells.Single(spell => spell.Id == "spell:guard:v2");
        saved.Document.GetProperty("name").GetString().ShouldBe("Guard");
        File.ReadAllText(Path.Combine(content.Path, "Spells", "brawler", "guard.v2.json")).ShouldContain("  \"id\": \"spell:guard:v2\"");
    }

    [Fact]
    public void Saving_a_document_with_a_mistyped_field_is_refused_before_it_reaches_the_disk()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        Should.Throw<InvalidGameContentException>(() => store.Save(ContentKind.Spell, "Spells/typo.v1.json", Guard.Replace("\"name\"", "\"nmae\"", StringComparison.Ordinal)))
            .Message.ShouldContain("nmae");
        File.Exists(Path.Combine(content.Path, "Spells", "typo.v1.json")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ContentKind.Spell, "../escape.json")]
    [InlineData(ContentKind.Spell, "Creatures/wrong-folder.json")]
    [InlineData(ContentKind.Spell, "Spells/not-json.txt")]
    public void Saving_outside_the_folder_of_its_kind_is_refused(ContentKind kind, string path)
    {
        using var content = new ContentDirectory().WithValidContent();

        Should.Throw<InvalidGameContentException>(() => new ContentStore(content.Path).Save(kind, path, Guard));
    }

    [Fact]
    public void Deleting_removes_the_file_and_refuses_one_that_is_not_there()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        store.Delete(ContentKind.Spell, "Spells/brawler/guard.v1.json");

        store.Read().Spells.ShouldHaveSingleItem().Id.ShouldBe("spell:strike:v1");
        Should.Throw<FileNotFoundException>(() => store.Delete(ContentKind.Spell, "Spells/brawler/guard.v1.json"));
    }

    [Fact]
    public void Aliases_are_rewritten_sorted_so_the_file_stays_reviewable()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        store.SaveAliases(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["spell:strike"] = "spell:strike:v2",
            ["spell:guard"] = "spell:guard:v1",
        });

        var written = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(content.Path, GameSchemaBuilder.AliasesFile)),
            GameSchemaJson.ReadOptions);
        written.ShouldNotBeNull().Keys.ShouldBe(["spell:guard", "spell:strike"]);
        store.Read().Aliases["spell:strike"].ShouldBe("spell:strike:v2");
    }

    [Fact]
    public void Building_writes_the_consolidated_schema_where_it_is_told()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        var (contentHash, notes) = store.Build(content.Output);

        notes.ShouldBeEmpty();
        contentHash.Length.ShouldBe(64);
        File.ReadAllText(Path.Combine(content.Output, GameSchemaJson.HashFileName)).ShouldBe(contentHash);
    }
}
