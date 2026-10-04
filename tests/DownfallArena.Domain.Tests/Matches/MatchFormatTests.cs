using DownfallArena.Domain.Matches;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class MatchFormatTests
{
    [Theory]
    [InlineData("3v3", 3)]
    [InlineData("2v2", 2)]
    [InlineData("1v1", 1)]
    [InlineData("3V3", 3)]
    [InlineData("  2v2  ", 2)]
    public void A_format_is_a_side_against_itself(string text, int teamSize) =>
        MatchFormat.Parse(text).TeamSize.ShouldBe(teamSize);

    /// <summary>A shell argument tends to be typed as the bare number, so the bare number names the format.</summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    public void The_team_size_alone_names_the_format(string text, int teamSize) =>
        MatchFormat.Parse(text).TeamSize.ShouldBe(teamSize);

    [Fact]
    public void A_format_reads_back_as_it_was_written() =>
        MatchFormat.Parse("2v2").ToString().ShouldBe("2v2");

    /// <summary>
    /// An asymmetric side would need a second team size on the rule set to mean anything, so it is refused
    /// rather than read as the left-hand number.
    /// </summary>
    [Theory]
    [InlineData("3v2")]
    [InlineData("0v0")]
    [InlineData("17v17")]
    [InlineData("threevthree")]
    [InlineData("v3")]
    [InlineData("3v")]
    [InlineData("-1")]
    [InlineData("3 v 3")]
    public void Anything_that_is_not_a_side_against_itself_is_refused(string text)
    {
        MatchFormat.TryParse(text, out _).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => MatchFormat.Parse(text));
    }

    [Fact]
    public void A_blank_name_is_no_format()
    {
        MatchFormat.TryParse(null, out _).ShouldBeFalse();
        MatchFormat.TryParse("  ", out _).ShouldBeFalse();
    }

    [Fact]
    public void A_team_size_outside_what_the_engine_encodes_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MatchFormat.OfTeamSize(0));
        Should.Throw<ArgumentOutOfRangeException>(() => MatchFormat.OfTeamSize(MatchFormat.MaxTeamSize + 1));
    }

    [Fact]
    public void A_rule_set_names_the_format_it_plays() =>
        MatchFormat.Of(RuleSet.Default).ShouldBe(MatchFormat.ThreeOnThree);

    [Fact]
    public void The_shortlist_a_chooser_offers_is_one_against_one_up_to_three_against_three() =>
        MatchFormat.Offered.Select(format => format.ToString()).ShouldBe(["1v1", "2v2", "3v3"]);
}
