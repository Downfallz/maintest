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
    private readonly TableRegistry _registry = new();

    public void Dispose()
    {
        _registry.Dispose();
        _hosted.Dispose();
    }

    [Fact]
    public async Task The_catalogue_carries_each_package_s_tags()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();

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
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var visual = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("packages").EnumerateArray().Single().GetProperty("visual");
        visual.GetProperty("family").GetString().ShouldBe("brute");
        visual.GetProperty("motif").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>The battle draws a confirmed cast from what the card says (ADR 0131); a card nobody drew says nothing.</summary>
    [Fact]
    public async Task The_catalogue_carries_how_each_card_s_cast_is_drawn()
    {
        var table = await _hosted.Composer.ComposeAsync(HostedTables.OnePerson, TestContext.Current.CancellationToken);
        _registry.TryAdd(table).ShouldBeTrue();

        var answer = await table.Api.HandleAsync("GET", "/api/catalogue", string.Empty, table.Seats[0].Token);

        answer.Status.ShouldBe(200);
        var cards = JsonDocument.Parse(Encoding.UTF8.GetString(answer.Body)).RootElement.GetProperty("cards").EnumerateArray().ToList();
        var guard = cards.Single(card => card.GetProperty("id").GetString() == "spell:guard:v1").GetProperty("visual");
        guard.GetProperty("family").GetString().ShouldBe("physical");
        guard.GetProperty("motif").GetString().ShouldBe("plates");
        guard.GetProperty("delivery").GetString().ShouldBe("cast");
        cards.Single(card => card.GetProperty("id").GetString() == "spell:strike:v1").GetProperty("visual").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
