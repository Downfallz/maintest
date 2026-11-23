using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

public sealed class SignInAttemptsTests
{
    private const string Address = "203.0.113.7";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 21, 0, 0, TimeSpan.Zero);

    private static void Refused(SignInAttempts attempts, string address, DateTimeOffset at)
    {
        attempts.Admit(address, at).ShouldBeNull();
        attempts.Settle(address, at, SignInAttempts.Outcome.Refused);
    }

    [Fact]
    public void An_address_past_its_refused_count_is_told_how_long_until_its_oldest_refusal_leaves_the_window()
    {
        var attempts = new SignInAttempts();
        for (var refused = 0; refused < SignInAttempts.PerAddress; refused++)
        {
            Refused(attempts, Address, Now.AddSeconds(refused * 10));
        }

        attempts.Admit(Address, Now.AddSeconds(45)).ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Each_person_the_provider_vouches_for_spends_nothing_of_the_count()
    {
        var attempts = new SignInAttempts();
        for (var person = 0; person < 2 * SignInAttempts.PerAddress; person++)
        {
            attempts.Admit(Address, Now).ShouldBeNull();
            attempts.Settle(Address, Now, SignInAttempts.Outcome.Vouched, $"discord:{person}");
        }

        attempts.Admit(Address, Now).ShouldBeNull();
    }

    [Fact]
    public void The_same_person_vouched_for_again_within_the_window_is_counted_as_a_refusal()
    {
        var attempts = new SignInAttempts();
        for (var again = 0; again <= SignInAttempts.PerAddress; again++)
        {
            attempts.Admit(Address, Now).ShouldBeNull();
            attempts.Settle(Address, Now, SignInAttempts.Outcome.Vouched, "discord:80351110224678912");
        }

        attempts.Admit(Address, Now).ShouldNotBeNull();
    }

    [Fact]
    public void An_address_is_vouched_for_at_most_so_many_people_a_window()
    {
        var attempts = new SignInAttempts();
        for (var person = 0; person < SignInAttempts.VouchedPerAddress; person++)
        {
            attempts.Admit(Address, Now).ShouldBeNull();
            attempts.Settle(Address, Now, SignInAttempts.Outcome.Vouched, $"discord:{person}");
        }

        attempts.Admit(Address, Now).ShouldBe(SignInAttempts.Window);
    }

    [Fact]
    public void An_address_waits_a_second_while_as_many_of_its_sign_ins_as_it_may_have_are_still_being_checked()
    {
        var attempts = new SignInAttempts();
        for (var asked = 0; asked < SignInAttempts.AtOnce; asked++)
        {
            attempts.Admit(Address, Now).ShouldBeNull();
        }

        attempts.Admit(Address, Now).ShouldBe(TimeSpan.FromSeconds(1));
        attempts.Settle(Address, Now, SignInAttempts.Outcome.Vouched, "discord:1");
        attempts.Admit(Address, Now).ShouldBeNull();
    }

    [Fact]
    public void A_sign_in_the_provider_could_not_check_is_not_counted()
    {
        var attempts = new SignInAttempts();
        for (var tried = 0; tried < 2 * SignInAttempts.PerAddress; tried++)
        {
            attempts.Admit(Address, Now).ShouldBeNull();
            attempts.Settle(Address, Now, SignInAttempts.Outcome.Unchecked);
        }

        attempts.Admit(Address, Now).ShouldBeNull();
    }

    [Fact]
    public void A_noisy_64_spends_its_own_count_and_leaves_its_neighbours_in_the_48_theirs()
    {
        var attempts = new SignInAttempts();
        for (var refused = 0; refused < SignInAttempts.PerAddress; refused++)
        {
            Refused(attempts, "2001:db8:aa:1::/64", Now);
        }

        attempts.Admit("2001:db8:aa:1::/64", Now).ShouldNotBeNull();
        attempts.Admit("2001:db8:aa:2::/64", Now).ShouldBeNull();
    }

    [Fact]
    public void A_48_whose_64s_were_refused_past_its_own_count_is_refused_whole()
    {
        var attempts = new SignInAttempts();
        for (var refused = 0; refused < SignInAttempts.PerNetwork; refused++)
        {
            Refused(attempts, $"2001:db8:aa:{refused:x}::/64", Now);
        }

        attempts.Admit("2001:db8:aa:ff::/64", Now).ShouldNotBeNull();
        attempts.Admit("2001:db8:ab::/64", Now).ShouldBeNull();
    }

    [Fact]
    public void The_addresses_whose_attempts_all_left_the_window_are_let_go_of()
    {
        var attempts = new SignInAttempts();
        for (var address = 0; address < 50; address++)
        {
            Refused(attempts, $"203.0.113.{address}", Now);
        }

        attempts.Admit("198.51.100.4", Now + SignInAttempts.Window);

        attempts.Held.ShouldBe(1);
    }
}
