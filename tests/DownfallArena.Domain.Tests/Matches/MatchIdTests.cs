using DownfallArena.Domain.Matches;

namespace DownfallArena.Domain.Tests.Matches;

public sealed class MatchIdTests
{
    [Fact]
    public void New_ids_are_unique()
    {
        var first = MatchId.New();
        var second = MatchId.New();

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Ids_with_the_same_value_are_equal()
    {
        var value = Guid.CreateVersion7();

        new MatchId(value).ShouldBe(new MatchId(value));
    }

    [Fact]
    public void Id_renders_as_its_underlying_value()
    {
        var value = Guid.CreateVersion7();

        new MatchId(value).ToString().ShouldBe(value.ToString());
    }
}
