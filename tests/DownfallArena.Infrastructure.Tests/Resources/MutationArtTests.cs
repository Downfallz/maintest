using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// How each package shows on the creature that bought it (ADR 0133): checked against the packages, the
/// vocabularies and the root it descends from when the content is built, published beside the schema, and read
/// back by a host, the way the package tags and the spell art are.
/// </summary>
public sealed class MutationArtTests
{
    private const string Brute = """
        { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 1 }
        """;

    private const string Ironbound = """
        { "id": "tier:ironbound:v1", "name": "Ironbound", "level": 2, "prerequisites": ["tier:brute:v1"], "spells": ["spell:guard"], "initiativeBonus": 1 }
        """;

    private static ContentDirectory Content() => new ContentDirectory().WithValidContent()
        .WithFile("Tiers/brute.v1.json", Brute).WithFile("Tiers/ironbound.v1.json", Ironbound);

    [Fact]
    public void Art_is_published_beside_the_schema_and_read_back_by_package()
    {
        using var content = Content().WithFile("presentation/mutation-art.json",
            """{ "tier:brute:v1": { "family": "brute" }, "tier:ironbound:v1": { "family": "brute", "motif": "plates" } }""");
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        MutationArt.Publish(content.Path, output, schema);
        var read = MutationArt.ReadNextTo(Path.Combine(output, "game.schema.json"));

        read.Of(TierId.Parse("tier:brute:v1")).ShouldBe(new Application.Catalogue.MutationVisual("brute", null));
        read.Of(TierId.Parse("tier:ironbound:v1")).ShouldBe(new Application.Catalogue.MutationVisual("brute", "plates"));
        read.Of(TierId.Parse("tier:titan:v1")).ShouldBeNull();
    }

    [Fact]
    public void A_schema_published_without_art_reads_as_none()
    {
        using var content = Content();

        var read = MutationArt.ReadNextTo(Path.Combine(content.Path, "dst", "game.schema.json"));

        read.Of(TierId.Parse("tier:brute:v1")).ShouldBeNull();
    }

    [Fact]
    public void Art_for_a_package_the_content_does_not_author_is_refused()
    {
        using var content = Content().WithFile("presentation/mutation-art.json", """{ "tier:titan:v1": { "family": "brute", "motif": "reinforcement" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => MutationArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.ShouldHaveSingleItem().ShouldContain("'tier:titan:v1' is no package this content authors");
    }

    [Fact]
    public void A_root_with_an_accent_and_a_branch_without_one_are_both_refused()
    {
        using var content = Content().WithFile("presentation/mutation-art.json",
            """{ "tier:brute:v1": { "family": "brute", "motif": "plates" }, "tier:ironbound:v1": { "family": "brute" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => MutationArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.Count.ShouldBe(2);
        refused.Problems.ShouldContain(problem => problem.Contains("is a root and draws 'plates'", StringComparison.Ordinal));
        refused.Problems.ShouldContain(problem => problem.Contains("'tier:ironbound:v1' draws no accent", StringComparison.Ordinal));
    }

    [Fact]
    public void A_word_outside_the_vocabularies_is_refused()
    {
        using var content = Content().WithFile("presentation/mutation-art.json", """{ "tier:ironbound:v1": { "family": "golem", "motif": "rivets" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => MutationArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.Count.ShouldBe(2);
        refused.Problems.ShouldContain(problem => problem.Contains("'golem'", StringComparison.Ordinal));
        refused.Problems.ShouldContain(problem => problem.Contains("'rivets'", StringComparison.Ordinal));
    }

    /// <summary>A branch is drawn on the body of the root it descends from; another family's body would be a silhouette no purchase bought.</summary>
    [Fact]
    public void A_branch_of_another_family_than_its_root_is_refused()
    {
        using var content = Content().WithFile("presentation/mutation-art.json",
            """{ "tier:brute:v1": { "family": "brute" }, "tier:ironbound:v1": { "family": "predator", "motif": "plates" } }""");
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => MutationArt.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.ShouldHaveSingleItem().ShouldContain("descends from 'tier:brute:v1', which is of the family 'brute'");
    }
}
