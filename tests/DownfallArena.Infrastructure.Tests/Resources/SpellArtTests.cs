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

    private static string Staged(string beats) =>
        $$"""{ "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast", "staging": {{beats}} } }""";

    private static IReadOnlyList<string> ProblemsOf(string art)
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", art);
        var schema = GameSchemaBuilder.Build(content.Path);

        return Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema)).Problems;
    }

    [Fact]
    public void An_entry_without_staging_reads_back_with_none()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", Guard);
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        SpellArt.Publish(content.Path, output, schema);
        var read = SpellArt.ReadNextTo(Path.Combine(output, "game.schema.json"));

        read.Of(SpellId.Parse("spell:guard:v1")).ShouldNotBeNull().Staging.ShouldBeNull();
    }

    [Fact]
    public void A_valid_staging_is_published_and_read_back_in_order()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", Staged("""
            [ { "beat": "motif", "at": 840 },
              { "beat": "burst", "at": 840, "lasts": 120, "count": 8, "shape": "shard", "tint": "bone", "direction": "along", "spread": 50, "level": "feet" } ]
            """));
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        SpellArt.Publish(content.Path, output, schema);
        var staging = SpellArt.ReadNextTo(Path.Combine(output, "game.schema.json")).Of(SpellId.Parse("spell:guard:v1")).ShouldNotBeNull().Staging.ShouldNotBeNull();

        staging.Count.ShouldBe(2);
        staging[0].Beat.ShouldBe("motif");
        staging[0].At.ShouldBe(840);
        staging[0].Count.ShouldBeNull();
        staging[1].Beat.ShouldBe("burst");
        staging[1].At.ShouldBe(840);
        staging[1].Lasts.ShouldBe(120);
        staging[1].Count.ShouldBe(8);
        staging[1].Shape.ShouldBe("shard");
        staging[1].Tint.ShouldBe("bone");
        staging[1].Direction.ShouldBe("along");
        staging[1].Spread.ShouldBe(50);
        staging[1].Level.ShouldBe("feet");
        staging[0].Level.ShouldBeNull();
    }

    [Fact]
    public void An_empty_staging_is_refused()
    {
        ProblemsOf(Staged("[]")).ShouldContain(problem => problem.Contains("stages no beat", StringComparison.Ordinal) && problem.Contains("'spell:guard:v1'", StringComparison.Ordinal));
    }

    [Fact]
    public void More_beats_than_the_limit_are_refused()
    {
        var beats = "[" + string.Join(",", Enumerable.Repeat("""{ "beat": "glow", "at": 900 }""", SpellArt.MaxBeats + 1)) + "]";

        ProblemsOf(Staged(beats)).ShouldContain(problem => problem.Contains("'spell:guard:v1'", StringComparison.Ordinal) && problem.Contains($"{SpellArt.MaxBeats + 1}", StringComparison.Ordinal));
    }

    [Fact]
    public void A_beat_outside_the_vocabulary_is_refused()
    {
        var problems = ProblemsOf(Staged("""[ { "beat": "sparkle", "at": 900 } ]"""));

        problems.ShouldContain(problem => problem.Contains("'sparkle'", StringComparison.Ordinal) && problem.Contains("'spell:guard:v1'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_beat_without_a_name_is_refused()
    {
        ProblemsOf(Staged("""[ { "at": 900 } ]""")).ShouldContain(problem => problem.Contains("''", StringComparison.Ordinal) && problem.Contains("'spell:guard:v1'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "beat": "glow", "at": -1 }""", "'-1'")]
    [InlineData("""{ "beat": "glow", "at": 2540 }""", "'2540'")]
    [InlineData("""{ "beat": "glow", "at": 900, "lasts": 0 }""", "'0'")]
    [InlineData("""{ "beat": "glow", "at": 800, "lasts": 100 }""", "'100'")]
    [InlineData("""{ "beat": "glow", "at": 840, "lasts": 1701 }""", "'1701'")]
    [InlineData("""{ "beat": "burst", "at": 900, "on": "sky" }""", "'sky'")]
    [InlineData("""{ "beat": "burst", "at": 900, "shape": "star" }""", "'star'")]
    [InlineData("""{ "beat": "burst", "at": 900, "tint": "azure" }""", "'azure'")]
    [InlineData("""{ "beat": "burst", "at": 900, "blend": "multiply" }""", "'multiply'")]
    [InlineData("""{ "beat": "burst", "at": 900, "direction": "sideways" }""", "'sideways'")]
    [InlineData("""{ "beat": "burst", "at": 900, "level": "knees" }""", "'knees'")]
    [InlineData("""{ "beat": "burst", "at": 900, "count": 25 }""", "'25'")]
    [InlineData("""{ "beat": "burst", "at": 900, "count": 0 }""", "'0'")]
    [InlineData("""{ "beat": "burst", "at": 900, "spread": 361 }""", "'361'")]
    [InlineData("""{ "beat": "burst", "at": 900, "reach": 401 }""", "'401'")]
    [InlineData("""{ "beat": "glow", "at": 900, "strength": 0 }""", "'0'")]
    [InlineData("""{ "beat": "glow", "at": 900, "strength": 1.5 }""", "'1.5'")]
    public void A_beat_with_a_value_out_of_range_is_refused_naming_it(string beat, string value)
    {
        var problems = ProblemsOf(Staged($"[ {beat} ]"));

        problems.ShouldContain(problem => problem.Contains("'spell:guard:v1'", StringComparison.Ordinal) && problem.Contains(value, StringComparison.Ordinal));
    }

    [Fact]
    public void A_null_among_the_beats_is_refused_and_not_a_crash()
    {
        ProblemsOf(Staged("[ null ]")).ShouldContain(problem => problem.Contains("'spell:guard:v1'", StringComparison.Ordinal) && problem.Contains("null", StringComparison.Ordinal));
    }

    /// <summary>A beat with no start would otherwise be published as starting at 0, with nobody told.</summary>
    [Fact]
    public void A_beat_that_does_not_say_when_it_starts_is_refused()
    {
        ProblemsOf(Staged("""[ { "beat": "burst", "count": 8 } ]""")).ShouldContain(problem => problem.Contains("'spell:guard:v1'", StringComparison.Ordinal) && problem.Contains("no 'at'", StringComparison.Ordinal));
    }

    /// <summary>The page refuses it, so the builder must: the sum of the start and the length must not wrap around.</summary>
    [Fact]
    public void A_beat_that_lasts_past_what_a_number_holds_is_refused()
    {
        ProblemsOf(Staged("""[ { "beat": "glow", "at": 900, "lasts": 2147483647 } ]""")).ShouldContain(problem => problem.Contains("'2147483647'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_misspelt_staging_refuses_the_file_and_is_not_dropped()
    {
        using var content = new ContentDirectory().WithValidContent()
            .WithFile("presentation/spell-art.json", """{ "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast", "stagin": [ { "beat": "glow", "at": 900 } ] } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));
    }

    [Theory]
    [InlineData("""{ "beat": "glow", "at": 0, "lasts": 840 }""")]
    [InlineData("""{ "beat": "glow", "at": 840, "lasts": 1700 }""")]
    [InlineData("""{ "beat": "burst", "at": 2539, "count": 24, "spread": 360, "reach": 400, "strength": 1 }""")]
    public void A_beat_at_the_edge_of_its_limits_is_accepted(string beat)
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", Staged($"[ {beat} ]"));
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        SpellArt.Publish(content.Path, output, schema);

        SpellArt.ReadNextTo(Path.Combine(output, "game.schema.json")).Of(SpellId.Parse("spell:guard:v1")).ShouldNotBeNull().Staging.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("motif")]
    [InlineData("flash")]
    [InlineData("shake")]
    public void A_beat_that_may_appear_only_once_is_refused_twice(string beat)
    {
        var problems = ProblemsOf(Staged($$"""[ { "beat": "{{beat}}", "at": 900 }, { "beat": "{{beat}}", "at": 1000 } ]"""));

        problems.ShouldContain(problem => problem.Contains($"'{beat}'", StringComparison.Ordinal) && problem.Contains("'spell:guard:v1'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_motif_beat_before_the_impact_window_is_refused()
    {
        var problems = ProblemsOf(Staged("""[ { "beat": "motif", "at": 839 } ]"""));

        problems.ShouldContain(problem => problem.Contains("'839'", StringComparison.Ordinal) && problem.Contains("'spell:guard:v1'", StringComparison.Ordinal));
    }

    [Fact]
    public void A_beat_with_a_property_the_kit_does_not_know_refuses_the_file()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("presentation/spell-art.json", Staged("""[ { "beat": "glow", "at": 900, "colour": "red" } ]"""));
        var schema = GameSchemaBuilder.Build(content.Path);

        Should.Throw<InvalidGameContentException>(() => SpellArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));
    }
}
