using DownfallArena.Application.Accounts;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class MatchResultTests
{
    private static readonly AccountId Mark = AccountId.New();
    private static readonly AccountId Bastien = AccountId.New();

    private static MatchResult Between(ResultSeat first, ResultSeat second) =>
        new(Guid.CreateVersion7().ToString(), DateTimeOffset.UnixEpoch, "3v3", "at-activation", "hash", 9, "player1", [first, second]);

    [Fact]
    public void Two_accounts_against_each_other_are_ranked()
    {
        Between(new("player1", Mark, null), new("player2", Bastien, null)).Ranked.ShouldBeTrue();
    }

    [Fact]
    public void A_bot_a_seat_without_an_account_or_one_account_on_both_sides_is_not_ranked()
    {
        Between(new("player1", Mark, null), new("player2", null, "greedy")).Ranked.ShouldBeFalse();
        Between(new("player1", Mark, null), new("player2", null, null)).Ranked.ShouldBeFalse();
        Between(new("player1", Mark, null), new("player2", Mark, null)).Ranked.ShouldBeFalse();
    }

    [Fact]
    public void A_game_opened_as_unranked_between_two_accounts_is_not_ranked()
    {
        var casual = Between(new("player1", Mark, null), new("player2", Bastien, null)) with { Casual = true };

        casual.Ranked.ShouldBeFalse();
    }
}
