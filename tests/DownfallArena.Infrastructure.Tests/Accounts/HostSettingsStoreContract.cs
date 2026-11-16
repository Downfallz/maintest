using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;

namespace DownfallArena.Infrastructure.Tests.Accounts;

/// <summary>What every host settings store promises (ADR 0177), played against each adapter.</summary>
public abstract class HostSettingsStoreContract
{
    private static readonly DateTimeOffset At = new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    protected abstract Task<IHostSettingsStore> StoreAsync();

    [Fact]
    public async Task A_host_that_never_saved_has_no_settings()
    {
        var store = await StoreAsync();

        (await store.GetAsync(Cancel)).ShouldBeNull();
    }

    [Fact]
    public async Task Settings_read_back_as_they_were_saved_and_a_save_replaces_the_one_before()
    {
        var store = await StoreAsync();
        var closed = new HostSettings(RegistrationOpen: false, "Maintenance at 22:00 UTC.", "mark", At);

        await store.SaveAsync(closed, Cancel);
        (await store.GetAsync(Cancel)).ShouldBe(closed);

        var reopened = new HostSettings(RegistrationOpen: true, Notice: null, "ops", At.AddHours(1));
        await store.SaveAsync(reopened, Cancel);
        (await store.GetAsync(Cancel)).ShouldBe(reopened);
    }
}
