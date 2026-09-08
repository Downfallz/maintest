using DownfallArena.Application.Learning;

namespace DownfallArena.Application.Tests.Learning;

public sealed class EngineVersionTests
{
    [Theory]
    [InlineData("1.0.0+abc123def456", "abc123def456", false)]
    [InlineData("1.0.0+abc123def456-dirty", "abc123def456", true)]
    [InlineData("1.0.0-preview+abc123def456", "abc123def456", false)]
    public void The_commit_and_the_dirty_flag_come_after_the_plus(string informationalVersion, string commit, bool dirty)
    {
        var version = EngineVersion.Parse(informationalVersion);

        version.ShouldBe(new EngineVersion(commit, dirty));
        version.IsKnown.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+-dirty")]
    public void A_version_without_a_commit_is_unknown(string? informationalVersion)
    {
        var version = EngineVersion.Parse(informationalVersion);

        version.Commit.ShouldBe(EngineVersion.Unknown);
        version.IsKnown.ShouldBeFalse();
    }

    [Fact]
    public void The_text_form_round_trips_through_the_informational_version()
    {
        new EngineVersion("abc123def456", true).ToString().ShouldBe("abc123def456-dirty");
        new EngineVersion("abc123def456", false).ToString().ShouldBe("abc123def456");
        EngineVersion.Parse("1.0.0+" + new EngineVersion("abc123def456", true)).ShouldBe(new EngineVersion("abc123def456", true));
    }

    [Fact]
    public void The_current_version_is_read_from_the_built_assembly()
    {
        var current = EngineVersion.Current;

        current.ShouldNotBeNull();
        current.Commit.ShouldNotBeNullOrWhiteSpace();
        EngineVersion.Parse("1.0.0+" + current).ShouldBe(current);
    }
}
