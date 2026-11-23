using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A push sender that keeps what it was asked to send, and sends nothing.</summary>
internal sealed class RecordedPush : IPushSender
{
    private readonly TaskCompletionSource _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<(string Endpoint, string Payload, string Topic)> Sent { get; } = [];

    /// <summary>Done once the first send is kept: what a test waits on rather than looking at <see cref="Sent" /> on a timer (#1187).</summary>
    public Task FirstSent => _first.Task;

    /// <summary>The endpoints the push service answers gone (410): sent to, and refused.</summary>
    public HashSet<string> Gone { get; } = [];

    /// <summary>How long each send takes before it is kept, as a push service answering slowly; none unless given.</summary>
    public TimeSpan Lag { get; init; }

    /// <summary>How many sends were kept so far, read under the same lock the sends are kept under.</summary>
    public int Count
    {
        get
        {
            lock (Sent)
            {
                return Sent.Count;
            }
        }
    }

    public async Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
    {
        if (Lag > TimeSpan.Zero)
        {
            await Task.Delay(Lag, cancellationToken);
        }

        lock (Sent)
        {
            Sent.Add((to.Endpoint, payload, topic));
            _first.TrySetResult();
            return !Gone.Contains(to.Endpoint);
        }
    }
}

/// <summary>An account's devices kept in memory, for the notices sent to them.</summary>
internal sealed class InMemoryDeviceStore : IDeviceStore
{
    /// <summary>A device's public key and secret as a browser gives them, in the shape the notices check.</summary>
    public const string Key = "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";

    public const string Auth = "AAECAwQFBgcICQoLDA0ODw";

    private readonly Dictionary<AccountId, (IReadOnlyList<PushDevice> Devices, string Version)> _held = [];
    private long _written;

    public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            return Task.FromResult(_held.TryGetValue(account, out var held) ? held.Devices : []);
        }
    }

    public Task<DeviceListRead> ReadAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            return Task.FromResult(_held.TryGetValue(account, out var held) ? new DeviceListRead(held.Devices, held.Version) : new DeviceListRead([], null));
        }
    }

    public Task<bool> SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, string? version, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            if ((_held.TryGetValue(account, out var held) ? held.Version : null) != version)
            {
                return Task.FromResult(false);
            }

            _held[account] = ([.. devices], $"{++_written}");
            return Task.FromResult(true);
        }
    }

    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            _held.Remove(account);
        }

        return Task.CompletedTask;
    }
}
