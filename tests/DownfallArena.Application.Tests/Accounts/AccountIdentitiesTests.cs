using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class AccountIdentitiesTests
{
    private static readonly ExternalIdentity Google = new(ExternalIdentity.Google, "109876543210987654321");
    private static readonly ExternalIdentity Discord = new("discord", "80351110224678912");

    [Fact]
    public void An_account_starts_with_the_identity_it_registered_with()
    {
        var identities = AccountIdentities.Of(Google);

        identities.All.ShouldBe([Google]);
        identities.Contains(Google).ShouldBeTrue();
        identities.Contains(Discord).ShouldBeFalse();
    }

    [Fact]
    public void Adding_an_identity_keeps_the_ones_before_it_in_order()
    {
        AccountIdentities.Of(Google).With(Discord).All.ShouldBe([Google, Discord]);
    }

    [Fact]
    public void Adding_an_identity_the_account_holds_changes_nothing()
    {
        var identities = AccountIdentities.Of(Google).With(Discord);

        identities.With(Google).ShouldBe(identities);
    }

    [Fact]
    public void Two_lists_of_the_same_identities_are_equal()
    {
        var first = AccountIdentities.Of(Google).With(Discord);
        var second = AccountIdentities.Restore([Google, Discord]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ShouldNotBe(AccountIdentities.Of(Google));
    }

    [Fact]
    public void Removing_an_identity_keeps_the_others()
    {
        var removed = AccountIdentities.Of(Google).With(Discord).Without(Google);

        removed.Value.All.ShouldBe([Discord]);
    }

    [Fact]
    public void The_last_identity_cannot_be_removed()
    {
        AccountIdentities.Of(Google).Without(Google).Error.ShouldBe(AccountErrors.LastIdentity);
    }

    [Fact]
    public void Removing_an_identity_the_account_does_not_hold_is_refused()
    {
        AccountIdentities.Of(Google).With(Discord).Without(new ExternalIdentity("discord", "1")).Error.ShouldBe(AccountErrors.IdentityNotLinked);
    }

    [Fact]
    public void A_stored_list_with_no_identity_or_one_twice_is_broken_data()
    {
        Should.Throw<ArgumentException>(() => AccountIdentities.Restore([]));
        Should.Throw<ArgumentException>(() => AccountIdentities.Restore([Google, Google]));
    }
}
