using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class NoticePreferencesTests
{
    [Fact]
    public void An_account_that_never_chose_lets_every_kind_pop_up() =>
        Enum.GetValues<NoticeKind>().ShouldAllBe(kind => NoticePreferences.Default.Pops(kind));

    [Theory]
    [InlineData(NoticeKind.Turn, true, false, false)]
    [InlineData(NoticeKind.Invitation, false, true, false)]
    [InlineData(NoticeKind.FriendRequest, false, true, false)]
    [InlineData(NoticeKind.FriendAccepted, false, true, false)]
    [InlineData(NoticeKind.Declined, false, true, false)]
    [InlineData(NoticeKind.SeatTaken, false, true, false)]
    [InlineData(NoticeKind.Left, false, false, true)]
    [InlineData(NoticeKind.MatchOver, false, false, true)]
    public void Each_kind_pops_up_by_the_one_choice_it_belongs_to(NoticeKind kind, bool turns, bool invitations, bool results)
    {
        var only = new NoticePreferences(turns, invitations, results);

        only.Pops(kind).ShouldBeTrue();
        new NoticePreferences(!turns, !invitations, !results).Pops(kind).ShouldBeFalse();
    }
}
