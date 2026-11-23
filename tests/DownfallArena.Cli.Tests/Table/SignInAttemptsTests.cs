using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

public sealed class SignInAttemptsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_address_past_its_count_is_told_how_long_until_its_oldest_attempt_leaves_the_window()
    {
        var attempts = new SignInAttempts();
        for (var taken = 0; taken < SignInAttempts.PerAddress; taken++)
        {
            attempts.Take("203.0.113.7", Now.AddSeconds(taken * 10)).ShouldBeNull();
        }

        attempts.Take("203.0.113.7", Now.AddSeconds(45)).ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void An_attempt_given_back_frees_its_place()
    {
        var attempts = new SignInAttempts();
        for (var taken = 0; taken < SignInAttempts.PerAddress; taken++)
        {
            attempts.Take("203.0.113.7", Now).ShouldBeNull();
        }

        attempts.GiveBack("203.0.113.7", Now);

        attempts.Take("203.0.113.7", Now).ShouldBeNull();
    }

    [Fact]
    public void The_64s_of_one_48_share_a_count()
    {
        var attempts = new SignInAttempts();
        for (var taken = 0; taken < SignInAttempts.PerAddress; taken++)
        {
            attempts.Take($"2001:db8:aa:{taken:x}::/64", Now).ShouldBeNull();
        }

        attempts.Take("2001:db8:aa:ff::/64", Now).ShouldNotBeNull();
        attempts.Take("2001:db8:ab::/64", Now).ShouldBeNull();
    }

    [Fact]
    public void The_addresses_whose_attempts_all_left_the_window_are_let_go_of()
    {
        var attempts = new SignInAttempts();
        for (var address = 0; address < 50; address++)
        {
            attempts.Take($"203.0.113.{address}", Now);
        }

        attempts.Take("198.51.100.4", Now + SignInAttempts.Window);

        attempts.Held.ShouldBe(1);
    }
}
