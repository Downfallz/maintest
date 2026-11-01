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
}
