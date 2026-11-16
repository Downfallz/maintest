using DownfallArena.Application.Hosting;

namespace DownfallArena.Application.Tests.Hosting;

public sealed class HostSettingsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_host_that_never_saved_its_settings_has_registration_open_and_no_notice()
    {
        HostSettings.Default.RegistrationOpen.ShouldBeTrue();
        HostSettings.Default.Notice.ShouldBeNull();
    }

    [Fact]
    public void Saved_settings_say_who_saved_them_and_when()
    {
        var saved = HostSettings.Create(registrationOpen: false, "Maintenance at 22:00 UTC.", "mark", At).Value;

        saved.ShouldBe(new HostSettings(false, "Maintenance at 22:00 UTC.", "mark", At));
    }

    [Fact]
    public void A_notice_is_made_one_line_with_its_spaces_collapsed()
    {
        var saved = HostSettings.Create(true, "  New version:\n  the   battle view\tis the default. ", "mark", At).Value;

        saved.Notice.ShouldBe("New version: the battle view is the default.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n ")]
    public void An_empty_notice_is_no_notice(string? notice)
    {
        HostSettings.Create(true, notice, "mark", At).Value.Notice.ShouldBeNull();
    }

    [Fact]
    public void A_notice_longer_than_a_line_is_refused()
    {
        var refused = HostSettings.Create(true, new string('a', HostSettings.LongestNotice + 1), "mark", At);

        refused.IsFailure.ShouldBeTrue();
        refused.Error.ShouldBe(HostSettings.NoticeTooLong);
    }

    [Fact]
    public void A_notice_of_exactly_the_longest_length_is_kept()
    {
        HostSettings.Create(true, new string('a', HostSettings.LongestNotice), "mark", At).IsSuccess.ShouldBeTrue();
    }
}
