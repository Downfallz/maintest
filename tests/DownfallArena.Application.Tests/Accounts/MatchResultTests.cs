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

    [Fact]
    public void A_game_the_operator_voided_between_two_accounts_is_not_ranked()
    {
        var voided = Between(new("player1", Mark, null), new("player2", Bastien, null)) with { Voided = new MatchVoid("abuse", "A player used a bug to win.", "op", DateTimeOffset.UnixEpoch) };

        voided.Ranked.ShouldBeFalse();
    }

    [Theory]
    [InlineData("abuse", "A player used a bug to win.", true)]
    [InlineData(" STUCK ", "The match stopped answering.", true)]
    [InlineData("bored", "A player used a bug to win.", false)]
    [InlineData("abuse", "too short", false)]
    [InlineData(null, "A player used a bug to win.", false)]
    public void A_void_needs_a_known_category_and_an_explanation_of_ten_to_five_hundred_characters(string? category, string explanation, bool made)
    {
        MatchVoid.Create(category, explanation, "op", DateTimeOffset.UnixEpoch).IsSuccess.ShouldBe(made);
    }

    [Fact]
    public void An_explanation_past_five_hundred_characters_is_refused()
    {
        MatchVoid.Create("other", new string('x', 501), "op", DateTimeOffset.UnixEpoch).Error.ShouldBe(MatchVoid.Invalid);
    }
}
