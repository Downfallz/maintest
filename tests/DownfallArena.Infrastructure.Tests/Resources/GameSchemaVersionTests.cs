using System.Security.Cryptography;
using System.Text;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Resources.Schema;

namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// The consolidated document's version against what it carries. The content hash is taken over this document,
/// so adding a member to it is a compatibility change: every catalogue built before the member hashes
/// differently the moment the member reaches the canonical form, and <see cref="GameSchemaBuilder.Load"/>
/// refuses a catalogue whose stored hash no longer matches its content. These tests pin the shape that keeps
/// the two versions apart.
/// </summary>
public sealed class GameSchemaVersionTests
{
    private const string Opener = """
        { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 1 }
        """;

    /// <summary>
    /// The canonical form of a catalogue without packages, written out so it can be read rather than trusted:
    /// these bytes are what the hash is taken over. Anything that leaks into this document -- an empty
    /// <c>tiers</c> member, a version that moved by accident -- fails here, which is the failure that would
    /// otherwise reach a catalogue built before the leak as "rebuild it with the data builder".
    /// <para>
    /// This pinned version 1 until ADR 0059, because a package-less catalogue was still byte for byte the
    /// document that version always was. Taking the per-spell <c>initiative</c> out of a spell ended that: the
    /// version moved to 3 on purpose, and so did every hash. The property is the same one -- these exact bytes,
    /// nothing else -- read against the shape the change decided on.
    /// </para>
    /// </summary>
    [Fact]
    public void A_catalogue_without_packages_hashes_exactly_the_document_written_here()
    {
        const string Canonical = """{"schemaVersion":3,"contentHash":"","creatures":[],"spells":[],"talentTrees":[],"aliases":{}}""";

        var hash = GameSchemaBuilder.ComputeHash(new GameSchema());

        hash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical))));
    }

    [Fact]
    public void A_built_catalogue_without_packages_declares_the_first_version_and_omits_the_member()
    {
        using var content = Content();

        var schema = GameSchemaBuilder.Build(content.Path);

        schema.SchemaVersion.ShouldBe(GameSchema.VersionWithoutTiers);
        schema.Tiers.ShouldBeNull();
    }

    [Fact]
    public void A_built_catalogue_with_packages_declares_the_version_that_carries_them()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", Opener);

        var schema = GameSchemaBuilder.Build(content.Path);

        schema.SchemaVersion.ShouldBe(GameSchema.VersionWithTiers);
        schema.Tiers.ShouldNotBeNull().Count.ShouldBe(1);
    }

    /// <summary>
    /// Every package disabled is a catalogue with no packages, so it is the older document rather than a newer
    /// one that happens to be empty. The authoring-only switch may not move the content hash (ADR 0015), and it
    /// would move it if the member stayed behind empty.
    /// </summary>
    [Fact]
    public void Disabling_every_package_leaves_the_document_at_the_first_version()
    {
        using var content = Content().WithFile("Tiers/spare.v1.json", """
            { "id": "tier:spare:v1", "name": "Spare", "level": 1, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 1, "enabled": false }
            """);
        using var bare = Content();

        var schema = GameSchemaBuilder.Build(content.Path);

        schema.SchemaVersion.ShouldBe(GameSchema.VersionWithoutTiers);
        schema.Tiers.ShouldBeNull();
        schema.ContentHash.ShouldBe(GameSchemaBuilder.Build(bare.Path).ContentHash);
    }

    [Fact]
    public void A_written_catalogue_without_packages_loads_back()
    {
        using var content = Content();

        var path = Write(content, schema => schema);

        File.ReadAllText(path).ShouldNotContain("tiers");
        GameSchemaBuilder.Load(path).Tiers.ShouldBeEmpty();
    }

    [Fact]
    public void A_written_catalogue_with_packages_loads_back()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", Opener);

        var path = Write(content, schema => schema);

        GameSchemaBuilder.Load(path).Tiers.Count.ShouldBe(1);
    }

    /// <summary>A version that disagrees with what the document carries is the one version it cannot have.</summary>
    [Fact]
    public void A_document_that_carries_packages_at_the_first_version_is_refused()
    {
        using var content = Content().WithFile("Tiers/brute.v1.json", Opener);

        var path = Write(content, schema => schema with { SchemaVersion = GameSchema.VersionWithoutTiers });

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("carries evolution packages");
    }

    [Fact]
    public void A_document_with_no_packages_at_the_version_that_carries_them_is_refused()
    {
        using var content = Content();

        var path = Write(content, schema => schema with { SchemaVersion = GameSchema.VersionWithTiers });

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("carries no evolution packages");
    }

    /// <summary>
    /// Read before the hash: a document from a builder this engine does not know would otherwise be reported as
    /// content that does not match its own hash, which sends the reader looking for corruption.
    /// </summary>
    [Fact]
    public void A_document_from_a_newer_builder_is_refused_as_a_version_rather_than_as_a_bad_hash()
    {
        using var content = Content();

        var path = Write(content, schema => schema with { SchemaVersion = GameSchema.VersionWithTiers + 1 });

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("written by a newer builder");
    }

    /// <summary>
    /// And read before the document is deserialized, which is the case the version check exists for: a newer
    /// builder writes members this reader does not know, and the strict reader refuses an unknown member with a
    /// <c>JsonException</c> naming the member. The version is the useful half of that sentence, so it is read
    /// first. The test above only moves the number and keeps today's shape, so it never reaches this.
    /// </summary>
    [Fact]
    public void A_document_from_a_newer_builder_is_refused_before_its_unknown_members_are_read()
    {
        using var content = Content();
        var path = Path.Combine(content.Path, "newer.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 5, "contentHash": "", "creatures": [], "spells": [], "talentTrees": [],
              "aliases": {}, "somethingANewerBuilderWrites": [1, 2]
            }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("written by a newer builder");
    }

    /// <summary>
    /// The mirror of the newer-builder case, and the one ADR 0059 introduced: a document from *before* the
    /// per-spell <c>initiative</c> was removed carries a member this reader does not know. Without a version of
    /// its own for the new shape the document would declare a version this engine claims to read, and the
    /// strict reader would then throw a <c>JsonException</c> naming the member — the same misleading sentence
    /// the check ahead of it exists to replace.
    /// </summary>
    [Fact]
    public void A_document_from_an_older_builder_is_refused_as_a_version_rather_than_as_an_unknown_member()
    {
        using var content = Content();
        var path = Path.Combine(content.Path, "older.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 2, "contentHash": "", "creatures": [],
              "spells": [{ "id": "spell:guard:v1", "name": "Guard", "spellType": "Defensive",
                "creatureClass": "Creature", "initiative": 1, "energyCost": 0, "criticalChance": 0,
                "targeting": { "origin": "Self", "scope": "SingleTarget", "maxTargets": 1 },
                "effects": [{ "kind": "Damage", "amount": 1 }] }],
              "talentTrees": [], "aliases": {}
            }
            """);

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("written by an older builder");
    }

    /// <summary>
    /// The other half, and why the version had to move at all: an engine from before ADR 0059 knows versions 1
    /// and 2, and a document declaring one of those is one it will deserialize. The missing <c>initiative</c>
    /// would come back as 0 and be written into the canonical form, so its hash check would fail and report
    /// content that does not match its own hash — corruption, for a catalogue that is perfectly good.
    /// </summary>
    [Fact]
    public void A_catalogue_built_today_declares_a_version_no_engine_that_reads_a_spell_initiative_accepts()
    {
        using var bare = Content();
        using var packaged = Content().WithFile("Tiers/brute.v1.json", Opener);

        int[] beforeTheStatWasRemoved = [1, 2];

        beforeTheStatWasRemoved.ShouldNotContain(GameSchemaBuilder.Build(bare.Path).SchemaVersion);
        beforeTheStatWasRemoved.ShouldNotContain(GameSchemaBuilder.Build(packaged.Path).SchemaVersion);
    }

    /// <summary>A version that is not a whole number is not a version, and says so rather than reading oddly.</summary>
    [Fact]
    public void A_version_that_is_not_a_number_is_refused()
    {
        using var content = Content();
        var path = Path.Combine(content.Path, "odd.json");
        File.WriteAllText(path, """{ "schemaVersion": "two", "contentHash": "", "creatures": [], "spells": [], "talentTrees": [], "aliases": {} }""");

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("not a whole number");
    }

    /// <summary>A document that is not an object at all is refused before anything reads a property off it.</summary>
    [Fact]
    public void A_document_that_is_not_a_schema_is_refused()
    {
        using var content = Content();
        var path = Path.Combine(content.Path, "list.json");
        File.WriteAllText(path, "[1, 2, 3]");

        Should.Throw<InvalidGameContentException>(() => GameSchemaBuilder.Load(path))
            .Message.ShouldContain("does not contain a game schema");
    }

    private static ContentDirectory Content() => new ContentDirectory().WithValidContent();

    /// <summary>Builds the content, applies <paramref name="change"/>, writes it, and returns the document.</summary>
    private static string Write(ContentDirectory content, Func<GameSchema, GameSchema> change)
    {
        var directory = Path.Combine(content.Path, "dst");
        GameSchemaBuilder.Write(change(GameSchemaBuilder.Build(content.Path)), directory);
        return Path.Combine(directory, GameSchemaJson.SchemaFileName);
    }
}
