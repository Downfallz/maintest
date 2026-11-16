using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;
using DownfallArena.Cli.Table;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>
/// The host's settings as a process holds them (ADR 0177): read at most once a minute, so another process's save
/// arrives within one, held at once where they were saved, and kept as last read when the store cannot be read.
/// </summary>
public sealed class HostSettingsCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 2, 0, 0, TimeSpan.Zero);
    private static readonly HostSettings Closed = new(RegistrationOpen: false, "Maintenance at 22:00 UTC.", "mark", Start);

    private readonly Kept _store = new();
    private readonly MovableClock _clock = new(Start);

    [Fact]
    public async Task A_host_that_never_saved_reads_registration_open_and_no_notice()
    {
        (await new HostSettingsCache(_store, _clock).CurrentAsync()).ShouldBe(HostSettings.Default);
    }

    [Fact]
    public async Task Another_process_s_save_is_read_once_a_minute_has_passed_and_not_before()
    {
        var cache = new HostSettingsCache(_store, _clock);
        await cache.CurrentAsync();
        _store.Held = Closed;

        _clock.Advance(TimeSpan.FromSeconds(59));
        var early = await cache.CurrentAsync();
        _clock.Advance(TimeSpan.FromSeconds(1));
        var late = await cache.CurrentAsync();

        early.ShouldBe(HostSettings.Default);
        late.ShouldBe(Closed);
        _store.Reads.ShouldBe(2);
    }

    [Fact]
    public async Task Settings_saved_here_hold_at_once()
    {
        var cache = new HostSettingsCache(_store, _clock);
        await cache.CurrentAsync();

        await cache.SaveAsync(Closed);

        (await cache.CurrentAsync()).ShouldBe(Closed);
        _store.Held.ShouldBe(Closed);
    }

    /// <summary>
    /// A read that found the old settings and finished after a save made here does not put them back: closing
    /// registration holds in the process that closed it.
    /// </summary>
    [Fact]
    public async Task A_read_that_began_before_a_save_does_not_undo_it()
    {
        var cache = new HostSettingsCache(_store, _clock);
        var held = new TaskCompletionSource<HostSettings?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Holding = held;
        var reading = cache.CurrentAsync();

        await cache.SaveAsync(Closed);
        held.SetResult(HostSettings.Default);
        await reading;

        (await cache.CurrentAsync()).ShouldBe(Closed);
    }

    [Fact]
    public async Task A_store_that_cannot_be_read_keeps_the_settings_last_read()
    {
        _store.Held = Closed;
        var cache = new HostSettingsCache(_store, _clock);
        await cache.CurrentAsync();
        _store.Out = true;

        _clock.Advance(HostSettingsCache.ReadEvery);
        var read = await cache.CurrentAsync();

        read.ShouldBe(Closed);
    }

    private sealed class Kept : IHostSettingsStore
    {
        public HostSettings? Held { get; set; }

        public bool Out { get; set; }

        public int Reads { get; private set; }

        /// <summary>When set, the next read answers only once the test completes it: a read still on its way.</summary>
        public TaskCompletionSource<HostSettings?>? Holding { get; set; }

        public Task<HostSettings?> GetAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            if (Holding is { } holding)
            {
                Holding = null;
                return holding.Task;
            }

            return Out ? throw new IOException("The store is out.") : Task.FromResult(Held);
        }

        public Task SaveAsync(HostSettings settings, CancellationToken cancellationToken = default)
        {
            Held = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
