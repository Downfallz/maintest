using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.SharedKernel.Tests.Identifiers;

public sealed class GuidIdentifierTests
{
    [Fact]
    public void New_match_ids_are_unique()
    {
        MatchId.New().ShouldNotBe(MatchId.New());
    }

    [Fact]
    public void Match_ids_with_the_same_value_are_equal()
    {
        var value = Guid.CreateVersion7();

        MatchId.From(value).ShouldBe(MatchId.From(value));
        MatchId.From(value).ToString().ShouldBe(value.ToString());
    }

    [Fact]
    public void A_match_id_cannot_be_the_empty_guid()
    {
        Should.Throw<ArgumentException>(() => MatchId.From(Guid.Empty));
    }

    [Fact]
    public void New_player_ids_are_unique()
    {
        PlayerId.New().ShouldNotBe(PlayerId.New());
    }

    [Fact]
    public void Player_ids_with_the_same_value_are_equal()
    {
        var value = Guid.CreateVersion7();

        PlayerId.From(value).ShouldBe(PlayerId.From(value));
        PlayerId.From(value).ToString().ShouldBe(value.ToString());
    }

    [Fact]
    public void A_player_id_cannot_be_the_empty_guid()
    {
        Should.Throw<ArgumentException>(() => PlayerId.From(Guid.Empty));
    }
}
