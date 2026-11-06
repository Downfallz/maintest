using DownfallArena.Application.Accounts;

namespace DownfallArena.Application.Tests.Accounts;

public sealed class GamePreferencesTests
{
    [Fact]
    public void A_new_account_reads_step_by_step_at_normal_speed_with_help_on()
    {
        GamePreferences.Default.ShouldBe(new GamePreferences(ReadingPace.Step, 1m, Help: true, ReducedMotion: false));
    }

    [Fact]
    public void Preferences_are_built_from_what_a_page_sends()
    {
        var made = GamePreferences.Create("auto", 1.5m, help: false, reducedMotion: true);

        made.IsSuccess.ShouldBeTrue();
        made.Value.ShouldBe(new GamePreferences(ReadingPace.Auto, 1.5m, Help: false, ReducedMotion: true));
    }

    [Theory]
    [InlineData("walk", 1)]
    [InlineData(null, 1)]
    [InlineData("step", 3)]
    [InlineData("step", 0)]
    public void A_pace_or_speed_the_settings_do_not_offer_is_refused(string? pace, double speed)
    {
        GamePreferences.Create(pace, (decimal)speed, help: true, reducedMotion: false).Error.ShouldBe(AccountErrors.PreferencesInvalid);
    }
}
