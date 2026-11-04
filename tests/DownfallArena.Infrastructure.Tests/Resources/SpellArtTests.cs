using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// How each spell's confirmed cast is drawn (ADR 0131): checked against the spells and the vocabularies when the
/// content is built, published beside the schema, and read back by a host, the way the package tags are.
/// </summary>
public sealed class SpellArtTests
{
    private const string Guard = """{ "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast" } }""";

    [Fact]
    public void Art_is_published_beside_the_schema_and_read_back_by_spell()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", Guard);
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        SpellArt.Publish(content.Path, output, schema);
        var read = SpellArt.ReadNextTo(Path.Combine(output, "game.schema.json"));

        read.Of(SpellId.Parse("spell:guard:v1")).ShouldBe(new Application.Catalogue.SpellVisual("physical", "plates", "cast"));
        read.Of(SpellId.Parse("spell:strike:v1")).ShouldBeNull();
    }

    [Fact]
    public void A_schema_published_without_art_reads_as_none()
    {
        using var content = new ContentDirectory().WithValidContent();

        var read = SpellArt.ReadNextTo(Path.Combine(content.Path, "dst", "game.schema.json"));

        read.Of(SpellId.Parse("spell:guard:v1")).ShouldBeNull();
    }

    [Fact]
    public void Art_for_a_spell_the_content_does_not_author_is_refused()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("presentation/spell-art.json", """{ "spell:meteor:v1": { "family": "elemental", "motif": "fire", "delivery": "cast" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.ShouldContain(problem => problem.Contains("'spell:meteor:v1' is no spell this content authors", StringComparison.Ordinal));
    }

    [Fact]
    public void A_word_outside_the_vocabularies_is_refused_with_every_problem_at_once()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("presentation/spell-art.json", """{ "spell:guard:v1": { "family": "holy", "motif": "halo", "delivery": "thrown" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.Count.ShouldBe(3);
        refused.Problems.ShouldContain(problem => problem.Contains("'holy'", StringComparison.Ordinal));
        refused.Problems.ShouldContain(problem => problem.Contains("'halo'", StringComparison.Ordinal));
        refused.Problems.ShouldContain(problem => problem.Contains("'thrown'", StringComparison.Ordinal));
    }

    /// <summary>A word left out is refused by name, not read as an empty family nobody notices.</summary>
    [Fact]
    public void A_visual_missing_a_word_is_refused()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("presentation/spell-art.json", """{ "spell:guard:v1": { "family": "physical", "motif": "plates" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.ShouldHaveSingleItem().ShouldContain("delivered as ''");
    }
}
