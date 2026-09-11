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

    /// <summary>
    /// Every authored file in the repository ends with a newline, and the hosted studio writes one too
    /// (ADR 0023). A save that dropped it turned a line nobody touched into a diff, and made the two backends
    /// disagree by one byte on the same document.
    /// </summary>
    [Fact]
    public void A_saved_file_ends_with_a_newline_like_every_authored_file()
    {
        using var content = new ContentDirectory();
        var store = new ContentStore(content.Path);

        store.Save(ContentKind.Spell, "Spells/brawler/guard.v2.json", Guard.Replace("spell:guard:v1", "spell:guard:v2", StringComparison.Ordinal));
        store.SaveAliases(new Dictionary<string, string>(StringComparer.Ordinal) { ["spell:guard"] = "spell:guard:v2" });

        File.ReadAllText(Path.Combine(content.Path, "Spells", "brawler", "guard.v2.json")).ShouldEndWith("\n");
        File.ReadAllText(Path.Combine(content.Path, "aliases.json")).ShouldEndWith("\n");
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

    /// <summary>
    /// The store deletes the one file it is told to; the alias and the references are the caller's to clean up,
    /// which is why the studio rewrites the alias map in the same action.
    /// </summary>
    [Fact]
    public void Deleting_removes_the_file_and_refuses_one_that_is_not_there()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        store.Delete(ContentKind.Spell, "Spells/brawler/guard.v1.json");

        var afterwards = store.Read();
        afterwards.Spells.ShouldHaveSingleItem().Id.ShouldBe("spell:strike:v1");
        afterwards.ContentHash.ShouldBeNull();
        afterwards.Problems.ShouldContain(problem => problem.Contains("spell:guard", StringComparison.Ordinal));
        Should.Throw<FileNotFoundException>(() => store.Delete(ContentKind.Spell, "Spells/brawler/guard.v1.json"));
    }

    [Fact]
    public void Reading_reports_what_the_enabled_switch_left_out()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("Spells/brawler/guard.v1.json", Guard.Replace("\"criticalChance\": 0", "\"criticalChance\": 0, \"enabled\": false", StringComparison.Ordinal));

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Spells.Single(spell => spell.Id == "spell:guard:v1").Enabled.ShouldBeFalse();
        catalogue.ContentHash.ShouldNotBeNull();
        catalogue.Notes.ShouldContain("Disabled spell 'spell:guard:v1' is not in the build.");
    }

    /// <summary>
    /// Cutting a version and creating an item both name a file the author believes is new. Replacing what is
    /// there would lose content, and the alias the same action repoints would stop naming it.
    /// </summary>
    [Fact]
    public void A_write_that_claims_to_be_new_refuses_to_replace_what_is_there()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);
        var path = Path.Combine(content.Path, "Spells", "brawler", "guard.v1.json");
        var before = File.ReadAllText(path);

        Should.Throw<InvalidGameContentException>(() => store.Save(ContentKind.Spell, "Spells/brawler/guard.v1.json", Guard, overwrite: false))
            .Message.ShouldContain("already exists");

        File.ReadAllText(path).ShouldBe(before);
    }

    [Fact]
    public void A_write_that_claims_to_be_new_still_writes_a_file_that_is_not_there()
    {
        using var content = new ContentDirectory().WithValidContent();

        new ContentStore(content.Path)
            .Save(ContentKind.Spell, "Spells/brawler/guard.v2.json", Guard.Replace("spell:guard:v1", "spell:guard:v2", StringComparison.Ordinal), overwrite: false)
            .ShouldBe("Spells/brawler/guard.v2.json");
    }

    [Fact]
    public void A_write_that_fails_leaves_no_temporary_file_behind()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);

        Should.Throw<InvalidGameContentException>(() => store.Save(ContentKind.Spell, "Spells/broken.v1.json", "{ \"nmae\": 1 }"));

        Directory.EnumerateFiles(content.Path, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
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
    public void A_content_directory_that_is_not_there_says_so()
    {
        Should.Throw<DirectoryNotFoundException>(() => new ContentStore(Path.Combine(AppContext.BaseDirectory, "nowhere")).Read())
            .Message.ShouldContain("does not exist");
    }

    [Fact]
    public void Content_folders_that_are_not_there_read_as_empty_rather_than_throwing()
    {
        using var content = new ContentDirectory().WithValidContent();
        Directory.Delete(Path.Combine(content.Path, GameSchemaBuilder.SpellsFolder), recursive: true);

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Spells.ShouldBeEmpty();
        catalogue.Creatures.ShouldHaveSingleItem();
        catalogue.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public void An_alias_map_that_does_not_parse_reads_as_no_aliases()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile(GameSchemaBuilder.AliasesFile, "{ not json");

        new ContentStore(content.Path).Read().Aliases.ShouldBeEmpty();
    }

    [Fact]
    public void Content_without_an_alias_map_reads_as_no_aliases()
    {
        using var content = new ContentDirectory().WithValidContent();
        File.Delete(Path.Combine(content.Path, GameSchemaBuilder.AliasesFile));

        new ContentStore(content.Path).Read().Aliases.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_alias_or_target_is_refused_so_the_map_stays_resolvable()
    {
        using var content = new ContentDirectory().WithValidContent();
        var store = new ContentStore(content.Path);
        var before = File.ReadAllText(Path.Combine(content.Path, GameSchemaBuilder.AliasesFile));

        Should.Throw<InvalidGameContentException>(() => store.SaveAliases(new Dictionary<string, string>(StringComparer.Ordinal) { ["spell:strike"] = " " }));
        Should.Throw<InvalidGameContentException>(() => store.SaveAliases(new Dictionary<string, string>(StringComparer.Ordinal) { [" "] = "spell:strike:v1" }));

        File.ReadAllText(Path.Combine(content.Path, GameSchemaBuilder.AliasesFile)).ShouldBe(before);
    }

    [Fact]
    public void An_empty_document_is_refused_rather_than_written_as_nothing()
    {
        using var content = new ContentDirectory().WithValidContent();

        Should.Throw<InvalidGameContentException>(() => new ContentStore(content.Path).Save(ContentKind.Spell, "Spells/empty.v1.json", "null"))
            .Message.ShouldContain("empty");
    }

    [Theory]
    [InlineData(ContentKind.Creature, "Creatures/other.v1.json")]
    [InlineData(ContentKind.TalentTree, "TalentTrees/other.v1.json")]
    public void Every_kind_is_written_into_its_own_folder(ContentKind kind, string path)
    {
        using var content = new ContentDirectory().WithValidContent();
        var document = kind == ContentKind.Creature
            ? """
                {
                  "id": "creature:other:v1", "name": "Other", "creatureClass": "Creature",
                  "baseHealth": 10, "baseEnergy": 0, "baseDefense": 0, "baseInitiative": 3, "baseCriticalChance": 0,
                  "talentTreeId": "talent-tree:base", "startingSpellIds": ["spell:strike"]
                }
                """
            : """
                { "id": "talent-tree:other:v1", "name": "Other", "root": { "code": "Other", "name": "Other" } }
                """;

        new ContentStore(content.Path).Save(kind, path, document).ShouldBe(path);

        File.Exists(Path.Combine(content.Path, path.Replace('/', Path.DirectorySeparatorChar))).ShouldBeTrue();
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
    private const string Knobs = """
        {
          "version": "knobs:v1",
          "objective": { "targets": [] },
          "constraints": {},
          "spells": { "spell:strike": { "intent": "The floor.", "knobs": [ { "path": "/energyCost", "min": 0, "max": 2, "step": 1 } ] } }
        }
        """;

    [Fact]
    public void The_balance_knobs_are_read_whole_and_not_interpreted()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("balance/knobs.json", Knobs);

        var balance = new ContentStore(content.Path).Read().Balance.ShouldNotBeNull();

        balance.GetProperty("version").GetString().ShouldBe("knobs:v1");
        balance.GetProperty("spells").GetProperty("spell:strike").GetProperty("knobs")
            .EnumerateArray().ShouldHaveSingleItem().GetProperty("path").GetString().ShouldBe("/energyCost");
    }

    [Fact]
    public void Content_with_no_knobs_file_reads_as_no_knobs_rather_than_as_a_problem()
    {
        using var content = new ContentDirectory().WithValidContent();

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Balance.ShouldBeNull();
        catalogue.Problems.ShouldBeEmpty();
        catalogue.Notes.ShouldNotContain(note => note.Contains("knobs.json", StringComparison.Ordinal));
    }

    /// <summary>
    /// The knobs are authoring metadata, not build input, so an unreadable one must not tell an author their
    /// content is broken. It is a note, the content still builds, and the page draws no knobs.
    /// </summary>
    [Fact]
    public void A_knobs_file_that_does_not_parse_is_a_note_and_not_a_problem()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("balance/knobs.json", "{ not json");

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Balance.ShouldBeNull();
        catalogue.ContentHash.ShouldNotBeNull();
        catalogue.Problems.ShouldBeEmpty();
        catalogue.Notes.ShouldContain(note => note.Contains("balance/knobs.json", StringComparison.Ordinal));
    }

    /// <summary>
    /// The whole reason the knobs may be keyed by alias and edited freely: the data builder reads Creatures,
    /// Spells, TalentTrees and aliases.json only, so no benchmark digest and no published hash moves when they do.
    /// </summary>
    [Fact]
    public void The_balance_knobs_do_not_move_the_content_hash()
    {
        using var without = new ContentDirectory().WithValidContent();
        using var with = new ContentDirectory().WithValidContent().WithFile("balance/knobs.json", Knobs);

        var bare = new ContentStore(without.Path).Read();
        var tuned = new ContentStore(with.Path).Read();

        tuned.Balance.ShouldNotBeNull();
        tuned.ContentHash.ShouldBe(bare.ContentHash.ShouldNotBeNull());
    }

    /// <summary>
    /// A knobs file the process may not read is the same answer as one that does not parse: a note, and no
    /// knobs. <see cref="UnauthorizedAccessException"/> is not an <see cref="IOException"/>, so it escapes a
    /// filter written for the readable-but-wrong case, and the catalogue route would answer 500 while
    /// <c>studio --export</c> ended outright — optional authoring metadata taking down a valid catalogue.
    /// <para>
    /// Skipped wherever reads cannot actually be denied: Windows, and every root user. The CI runner is
    /// neither, so this is a real assertion there and an honest nothing elsewhere, rather than a test that
    /// quietly proves nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void A_knobs_file_that_cannot_be_read_is_a_note_and_not_a_crash()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("balance/knobs.json", Knobs);
        if (!DenyReads(Path.Combine(content.Path, "balance", "knobs.json")))
        {
            Assert.Skip("Reads cannot be denied to this process, so there is nothing here to refuse it.");
        }

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Balance.ShouldBeNull();
        catalogue.ContentHash.ShouldNotBeNull();
        catalogue.Problems.ShouldBeEmpty();
        catalogue.Notes.ShouldContain(note => note.Contains("balance/knobs.json", StringComparison.Ordinal));
    }

    /// <summary>Takes every permission off a file, and says whether that actually stopped this process.</summary>
    private static bool DenyReads(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        File.SetUnixFileMode(path, UnixFileMode.None);
        try
        {
            File.ReadAllText(path);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
    /// <summary>
    /// The class promises reading never throws on a bad file. A spell the process cannot open was the one that
    /// did: the read sat outside the try, so it left <see cref="ContentStore.Read"/> entirely and took the whole
    /// catalogue with it rather than being listed like a file that does not parse.
    /// </summary>
    [Fact]
    public void A_spell_that_cannot_be_read_is_listed_with_its_problem_rather_than_thrown()
    {
        using var content = new ContentDirectory().WithValidContent();
        if (!DenyReads(Path.Combine(content.Path, "Spells", "strike.v1.json")))
        {
            Assert.Skip("Reads cannot be denied to this process, so there is nothing here to refuse it.");
        }

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Spells.Count.ShouldBe(2);
        catalogue.Spells.ShouldContain(spell => spell.Path == "Spells/strike.v1.json" && spell.Problem != null);
    }

    /// <summary>
    /// An alias map that cannot be read is not an empty one: every reference resolves through it, so falling
    /// back to none silently turns a permissions mistake into a catalogue where nothing is aliased at all.
    /// </summary>
    [Fact]
    public void An_alias_map_that_cannot_be_read_says_so_rather_than_reading_as_no_aliases()
    {
        using var content = new ContentDirectory().WithValidContent();
        if (!DenyReads(Path.Combine(content.Path, "aliases.json")))
        {
            Assert.Skip("Reads cannot be denied to this process, so there is nothing here to refuse it.");
        }

        var catalogue = new ContentStore(content.Path).Read();

        catalogue.Aliases.ShouldBeEmpty();
        catalogue.Notes.ShouldContain(note => note.Contains("aliases.json", StringComparison.Ordinal));
    }

}
