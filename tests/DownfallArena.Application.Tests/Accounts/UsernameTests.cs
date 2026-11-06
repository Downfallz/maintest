using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class UsernameTests
{
    [Theory]
    [InlineData("MarkF")]
    [InlineData("kodiak-7")]
    [InlineData("Lune_Noire")]
    [InlineData("abc")]
    [InlineData("a2345678901234567890")]
    public void A_name_of_letters_digits_underscores_and_hyphens_starting_with_a_letter_is_kept_as_typed(string typed)
    {
        Username.Create(typed).Value.Value.ShouldBe(typed);
    }

    [Fact]
    public void Spaces_around_a_name_are_not_part_of_it()
    {
        Username.Create("  MarkF ").Value.Value.ShouldBe("MarkF");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("a23456789012345678901")]
    [InlineData("7kodiak")]
    [InlineData("_mark")]
    [InlineData("Mark F")]
    [InlineData("Mark.F")]
    [InlineData("Médéric")]
    [InlineData("mаrk")]
    public void A_name_outside_the_rules_is_refused_as_invalid(string? typed)
    {
        Username.Create(typed).Error.ShouldBe(AccountErrors.UsernameInvalid);
    }

    [Theory]
    [InlineData("Greedy")]
    [InlineData("ADMIN")]
    [InlineData("DownfallInstinct")]
    [InlineData("player2")]
    [InlineData("you")]
    public void A_word_a_card_already_means_something_by_is_reserved_whatever_its_case(string typed)
    {
        Username.Create(typed).Error.ShouldBe(AccountErrors.UsernameReserved);
    }

    [Fact]
    public void Names_that_differ_only_by_case_are_the_same_name()
    {
        Username.Create("MarkF").Value.Key.ShouldBe(Username.Create("markf").Value.Key);
    }
}
