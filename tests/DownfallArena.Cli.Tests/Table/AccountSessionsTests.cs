using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

public sealed class AccountSessionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 20, 14, 0, TimeSpan.Zero);
    private static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];
    private static readonly ExternalIdentity Mark = new(ExternalIdentity.Google, "109876543210987654321");

    private readonly MovableClock _clock = new(Now);

    private static Account AnAccount(int generation = 1) =>
        new(AccountId.New(), Mark, Username.Create("MarkF").Value, Now, generation);

    private AccountSessions Sessions(byte[]? key = null) => new(key ?? Key, _clock);

    [Fact]
    public void A_session_reads_back_as_the_account_and_generation_it_was_issued_for()
    {
        var account = AnAccount(generation: 3);

        var claim = Sessions().Read(Sessions().Issue(account)).ShouldNotBeNull();

        claim.Account.ShouldBe(account.Id);
        claim.Generation.ShouldBe(3);
        claim.IssuedAt.ShouldBe(Now);
    }

    [Fact]
    public void A_session_another_key_signed_is_refused()
    {
        var other = Sessions([.. Enumerable.Repeat((byte)7, 32)]).Issue(AnAccount());

        Sessions().Read(other).ShouldBeNull();
    }

    [Fact]
    public void A_session_whose_account_was_changed_after_signing_is_refused()
    {
        var issued = Sessions().Issue(AnAccount());
        var forged = issued.Replace(issued.Split('.')[1], AccountId.New().Value, StringComparison.Ordinal);

        Sessions().Read(forged).ShouldBeNull();
    }

    [Fact]
    public void A_session_is_refused_once_its_lifetime_has_passed()
    {
        var issued = Sessions().Issue(AnAccount());
        _clock.Advance(AccountSessions.Lifetime - TimeSpan.FromMinutes(1));
        Sessions().Read(issued).ShouldNotBeNull();

        _clock.Advance(TimeSpan.FromMinutes(1));

        Sessions().Read(issued).ShouldBeNull();
    }

    [Fact]
    public void A_session_a_day_old_is_due_for_renewal()
    {
        var claim = Sessions().Read(Sessions().Issue(AnAccount()))!;
        Sessions().DueForRenewal(claim).ShouldBeFalse();

        _clock.Advance(AccountSessions.RenewAfter);

        Sessions().DueForRenewal(claim).ShouldBeTrue();
    }

    [Fact]
    public void A_ticket_reads_back_as_the_sign_in_it_holds_for_ten_minutes()
    {
        var ticket = Sessions().Ticket(Mark);
        Sessions().ReadTicket(ticket).ShouldBe(Mark);

        _clock.Advance(AccountSessions.TicketLifetime);

        Sessions().ReadTicket(ticket).ShouldBeNull();
    }

    [Fact]
    public void A_ticket_is_not_a_session_and_a_session_is_not_a_ticket()
    {
        Sessions().Read(Sessions().Ticket(Mark)).ShouldBeNull();
        Sessions().ReadTicket(Sessions().Issue(AnAccount())).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("s1")]
    [InlineData("s1.acct_aaaaaaaaaaaaaaaa.1.1.")]
    [InlineData("s1.acct_aaaaaaaaaaaaaaaa.1.1.!!!")]
    public void Text_that_is_not_a_session_is_refused_without_throwing(string? text)
    {
        Sessions().Read(text).ShouldBeNull();
        Sessions().ReadTicket(text).ShouldBeNull();
    }

    [Fact]
    public void The_cookie_keeps_the_session_out_of_scripts_and_is_secure_off_this_machine()
    {
        AccountSessions.SetCookie("value", secure: true).ShouldBe("downfall_session=value; Path=/; Max-Age=2592000; HttpOnly; SameSite=Lax; Secure");
        AccountSessions.SetCookie("value", secure: false).ShouldNotContain("Secure");
        AccountSessions.ClearCookie(secure: true).ShouldContain("Max-Age=0");
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
