using System.Text.Json;
using DownfallArena.Application.Hosting;
using DownfallArena.Application.Hosting.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The host's settings as this process knows them (ADR 0177): read from the store at most once every
/// <see cref="ReadEvery" />, so a change saved by another process of the host reaches this one within a minute, and at
/// once in the process that saved it. A store that cannot be read keeps the settings last read, or the defaults before
/// any were: registration open and no notice.
/// </summary>
internal sealed class HostSettingsCache(IHostSettingsStore store, TimeProvider clock)
{
    /// <summary>How long settings read are taken as they are before the store is read again.</summary>
    public static readonly TimeSpan ReadEvery = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private HostSettings _last = HostSettings.Default;
    private DateTimeOffset? _readAt;
    private Task<HostSettings>? _reading;

    // How many saves this process has made: a read that began before one finished stands for nothing newer than it.
    private long _saves;

    /// <summary>The settings, read again when the ones held are older than a minute; one read at a time.</summary>
    public Task<HostSettings> CurrentAsync()
    {
        lock (_gate)
        {
            if (_readAt is { } read && clock.GetUtcNow() - read < ReadEvery)
            {
                return Task.FromResult(_last);
            }

            if (_reading is { IsCompleted: false } reading)
            {
                return reading;
            }

            _reading = ReadAsync();
            return _reading;
        }
    }

    /// <summary>Saves the settings for every process of the host, and holds them here at once.</summary>
    public async Task SaveAsync(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await store.SaveAsync(settings);
        lock (_gate)
        {
            _saves++;
            _last = settings;
            _readAt = clock.GetUtcNow();
        }
    }

    private async Task<HostSettings> ReadAsync()
    {
        long saves;
        lock (_gate)
        {
            saves = _saves;
        }

        HostSettings? read = null;
        try
        {
            read = await store.GetAsync() ?? HostSettings.Default;
        }
        catch (Exception failure) when (TableRegistry.StoreFails(failure) || failure is JsonException)
        {
            Console.WriteLine($"  The host's settings could not be read ({failure.Message}): the ones last read stand.");
        }

        lock (_gate)
        {
            // A save made here while the store was being read is newer than what the read found: it stands.
            if (saves == _saves)
            {
                _last = read ?? _last;
                _readAt = clock.GetUtcNow();
            }

            return _last;
        }
    }
}
