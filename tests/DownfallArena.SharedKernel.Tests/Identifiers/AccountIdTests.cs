using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.SharedKernel.Tests.Identifiers;

public sealed class AccountIdTests
{
    [Fact]
    public void A_new_account_id_is_the_prefix_and_sixteen_base32_characters()
    {
        var id = AccountId.New().Value;

        id.ShouldStartWith("acct_");
        id.Length.ShouldBe(21);
        id[5..].ShouldAllBe(c => "abcdefghijklmnopqrstuvwxyz234567".Contains(c));
    }

    [Fact]
    public void New_account_ids_are_unique()
    {
        AccountId.New().ShouldNotBe(AccountId.New());
    }

    [Fact]
    public void An_account_id_parses_back_from_its_text()
    {
        var id = AccountId.New();

        AccountId.Parse(id.ToString()).ShouldBe(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("acct_")]
    [InlineData("acct_7kq2m9x4p1d8w3z")]
    [InlineData("acct_7kq2m9x4p1d8w3zcq")]
    [InlineData("acct_7KQ2M9X4P2D8W3ZC")]
    [InlineData("user_7kq2m9x4p2d8w3zc")]
    [InlineData("acct_7kq2m9x4p1d8w3zc")]
    public void Text_that_is_not_an_account_id_does_not_parse(string? text)
    {
        AccountId.TryParse(text, out _).ShouldBeFalse();
    }
}
