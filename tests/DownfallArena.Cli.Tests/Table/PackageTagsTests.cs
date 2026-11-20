using System.Text;
using System.Text.Json;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The words each package is known by reach the table's catalogue from beside the schema, where the build
/// published them: presentation, so they ride on the package cards without moving the content hash.
/// </summary>
public sealed class PackageTagsTests : IDisposable
{
    private readonly HostedTables _hosted = new();
    private readonly TableRegistry _registry;

    public PackageTagsTests()
    {
        _registry = _hosted.Registry();
    }

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    private async Task<PlayedTable> Opened()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using var place = (await _registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await _registry.AddAsync(table, place, TestContext.Current.CancellationToken);
        return table;
    }

    [Fact]
    public async Task The_catalogue_carries_each_package_s_tags()
    {
        var table = await Opened();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var package = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("packages").EnumerateArray().Single();
        package.GetProperty("id").GetString().ShouldBe("tier:guard:v1");
        package.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["Defense", "Sustain"]);
    }

    /// <summary>The battle shows a package on the creature that bought it from what the package says (ADR 0133).</summary>
    [Fact]
    public async Task The_catalogue_carries_how_each_package_shows_on_a_creature()
    {
        var table = await Opened();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var visual = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("packages").EnumerateArray().Single().GetProperty("visual");
        visual.GetProperty("family").GetString().ShouldBe("brute");
        visual.GetProperty("motif").ValueKind.ShouldBe(JsonValueKind.Null);
        visual.GetProperty("portrait").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The portrait a package's art names rides on the package's visual, as a card's does.</summary>
    [Fact]
    public async Task The_catalogue_carries_the_portrait_a_package_s_art_names()
    {
        using var hosted = new HostedTables(null, content: content => content.Write("presentation/mutation-art.json", """{ "tier:guard:v1": { "family": "brute", "portrait": "titan" } }"""));
        using var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await registry.AddAsync(table, place, TestContext.Current.CancellationToken);

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var visual = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("packages").EnumerateArray().Single().GetProperty("visual");
        visual.GetProperty("portrait").GetString().ShouldBe("titan");
    }

    /// <summary>The battle draws a confirmed cast from what the card says (ADR 0131); a card nobody drew says nothing.</summary>
    [Fact]
    public async Task The_catalogue_carries_how_each_card_s_cast_is_drawn()
    {
        var table = await Opened();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var cards = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("cards").EnumerateArray().ToList();
        var guard = cards.Single(card => card.GetProperty("id").GetString() == "spell:guard:v1").GetProperty("visual");
        guard.GetProperty("family").GetString().ShouldBe("physical");
        guard.GetProperty("motif").GetString().ShouldBe("plates");
        guard.GetProperty("delivery").GetString().ShouldBe("cast");
        cards.Single(card => card.GetProperty("id").GetString() == "spell:strike:v1").GetProperty("visual").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The staging a spell's art carries rides on the card's visual; an entry without one serves null.</summary>
    [Fact]
    public async Task The_catalogue_carries_the_staging_of_a_card_s_cast()
    {
        const string art = """
            {
              "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast", "staging": [ { "beat": "motif", "at": 840 }, { "beat": "burst", "at": 840, "direction": "along", "level": "feet" } ] },
              "spell:strike:v1": { "family": "physical", "motif": "plates", "delivery": "cast" }
            }
            """;
        using var hosted = new HostedTables(null, content: content => content.Write("presentation/spell-art.json", art));
        using var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await registry.AddAsync(table, place, TestContext.Current.CancellationToken);

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var cards = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("cards").EnumerateArray().ToList();
        var staging = cards.Single(card => card.GetProperty("id").GetString() == "spell:guard:v1").GetProperty("visual").GetProperty("staging");
        staging.ValueKind.ShouldBe(JsonValueKind.Array);
        staging[0].GetProperty("beat").GetString().ShouldBe("motif");
        staging[0].GetProperty("at").GetInt32().ShouldBe(840);
        staging[1].GetProperty("direction").GetString().ShouldBe("along");
        staging[1].GetProperty("level").GetString().ShouldBe("feet");
        cards.Single(card => card.GetProperty("id").GetString() == "spell:strike:v1").GetProperty("visual").GetProperty("staging").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The portrait a spell's art names rides on the card's visual; an entry without one serves null.</summary>
    [Fact]
    public async Task The_catalogue_carries_the_portrait_a_card_s_art_names()
    {
        const string art = """
            {
              "spell:guard:v1": { "family": "physical", "motif": "plates", "delivery": "cast", "portrait": "brace" },
              "spell:strike:v1": { "family": "physical", "motif": "plates", "delivery": "cast" }
            }
            """;
        using var hosted = new HostedTables(null, content: content => content.Write("presentation/spell-art.json", art));
        using var registry = hosted.Registry();
        var table = await hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        using var place = (await registry.ReserveAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await registry.AddAsync(table, place, TestContext.Current.CancellationToken);

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var cards = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("cards").EnumerateArray().ToList();
        cards.Single(card => card.GetProperty("id").GetString() == "spell:guard:v1").GetProperty("visual").GetProperty("portrait").GetString().ShouldBe("brace");
        cards.Single(card => card.GetProperty("id").GetString() == "spell:strike:v1").GetProperty("visual").GetProperty("portrait").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
