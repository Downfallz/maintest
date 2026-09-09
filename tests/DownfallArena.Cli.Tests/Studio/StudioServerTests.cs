using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Tests.Studio;

/// <summary>
/// The one piece of the host's routing that is worth reading on its own: which two runs a comparison path
/// names. Anything that is not exactly two is a 404 rather than a guess.
/// </summary>
public sealed class StudioServerTests
{
    [Theory]
    [InlineData("/compare/one/two", "one", "two")]
    [InlineData("/compare/one/two/", "one", "two")]
    public void A_comparison_path_names_its_two_runs(string path, string first, string second)
    {
        StudioServer.Comparison(path).ShouldBe((first, second));
    }

    [Theory]
    [InlineData("/compare/one")]
    [InlineData("/compare/one/two/three")]
    [InlineData("/compare/")]
    [InlineData("/compare")]
    [InlineData("/runs/one")]
    public void Anything_that_does_not_name_two_runs_is_not_a_comparison(string path)
    {
        StudioServer.Comparison(path).ShouldBeNull();
    }
}
