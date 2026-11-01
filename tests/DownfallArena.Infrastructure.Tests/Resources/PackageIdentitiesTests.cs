using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Infrastructure.Tests.Resources;

/// <summary>
/// The words each package is known by (docs/domain/package-identity-tags.md): checked against the packages and
/// the vocabulary when the content is built, published beside the schema, and read back by a host.
/// </summary>
public sealed class PackageIdentitiesTests
{
    private const string Brute = """
        { "id": "tier:brute:v1", "name": "Brute", "level": 1, "prerequisites": [], "spells": ["spell:guard"], "initiativeBonus": 1 }
        """;

    [Fact]
    public void Tags_are_published_beside_the_schema_and_read_back_by_package()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("Tiers/brute.v1.json", Brute)
            .WithFile("presentation/package-tags.json", """{ "tier:brute:v1": ["Pressure", "Defense"] }""");
        var output = Path.Combine(content.Path, "dst");
        var schema = GameSchemaBuilder.Build(content.Path);

        GameSchemaBuilder.Write(schema, output);
        PackageIdentities.Publish(content.Path, output, schema);
        var read = PackageIdentities.ReadNextTo(Path.Combine(output, "game.schema.json"));

        read.Of(TierId.Parse("tier:brute:v1")).ShouldBe(["Pressure", "Defense"]);
        read.Of(TierId.Parse("tier:titan:v1")).ShouldBeEmpty();
    }

    [Fact]
    public void A_schema_published_without_tags_reads_as_none()
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("Tiers/brute.v1.json", Brute);

        var read = PackageIdentities.ReadNextTo(Path.Combine(content.Path, "dst", "game.schema.json"));

        read.Of(TierId.Parse("tier:brute:v1")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "tier:nowhere:v1": ["Pressure", "Defense"] }""", "is no package of this content")]
    [InlineData("""{ "tier:brute:v1": ["Pressure"] }""", "has 1 tags")]
    [InlineData("""{ "tier:brute:v1": ["Pressure", "Defense", "Burst", "AoE"] }""", "has 4 tags")]
    [InlineData("""{ "tier:brute:v1": ["Pressure", "Tanky"] }""", "is tagged 'Tanky'")]
    [InlineData("""{ "tier:brute:v1": ["Pressure", "Pressure"] }""", "the same tag twice")]
    public void Tags_off_the_vocabulary_or_the_packages_are_refused(string authored, string problem)
    {
        using var content = new ContentDirectory().WithValidContent().WithFile("Tiers/brute.v1.json", Brute)
            .WithFile("presentation/package-tags.json", authored);
        var schema = GameSchemaBuilder.Build(content.Path);

        var refused = Should.Throw<InvalidGameContentException>(() => PackageIdentities.Publish(content.Path, Path.Combine(content.Path, "dst"), schema));

        refused.Problems.ShouldContain(line => line.Contains(problem, StringComparison.Ordinal));
    }

    /// <summary>The content as authored: every package but the three capstones is known by two or three words.</summary>
    [Fact]
    public void Every_authored_package_but_a_capstone_has_its_tags()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "data");
        var schema = GameSchemaBuilder.Build(data);
        var output = Path.Combine(Path.GetTempPath(), $"package-tags-{Guid.NewGuid():N}");

        PackageIdentities.Publish(data, output, schema);
        var read = PackageIdentities.ReadNextTo(Path.Combine(output, "game.schema.json"));

        var untagged = schema.Tiers!.Where(tier => read.Of(TierId.Parse(tier.Id)).Count == 0).Select(tier => tier.Name);
        untagged.ShouldBe(["Apex", "Archmage", "Titan"], ignoreOrder: true);
        Directory.Delete(output, recursive: true);
    }
}
