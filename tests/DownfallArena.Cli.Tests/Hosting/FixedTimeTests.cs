using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Tests.Hosting;

public sealed class FixedTimeTests
{
    [Fact]
    public void The_token_held_is_the_same_token() =>
        FixedTime.Same("token-of-the-practice", "token-of-the-practice").ShouldBeTrue();

    [Theory]
    [InlineData("token-of-the-practicf")]
    [InlineData("token-of-the")]
    [InlineData("TOKEN-OF-THE-PRACTICE")]
    public void Another_token_is_not_the_one_held(string supplied) =>
        FixedTime.Same(supplied, "token-of-the-practice").ShouldBeFalse();

    [Theory]
    [InlineData(null, "held")]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    [InlineData("held", null)]
    public void A_blank_token_is_nobody_s(string? supplied, string? held) =>
        FixedTime.Same(supplied, held).ShouldBeFalse();
}
