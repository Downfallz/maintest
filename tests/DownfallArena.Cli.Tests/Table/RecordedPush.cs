using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Table;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Tests.Table;

/// <summary>A push sender that keeps what it was asked to send, and sends nothing.</summary>
internal sealed class RecordedPush : IPushSender
{
    public List<(string Endpoint, string Payload, string Topic)> Sent { get; } = [];

    public Task<bool> SendAsync(PushSubscription to, string payload, string topic, CancellationToken cancellationToken = default)
    {
        lock (Sent)
        {
            Sent.Add((to.Endpoint, payload, topic));
        }

        return Task.FromResult(true);
    }
}

/// <summary>An account's devices kept in memory, for the notices sent to them.</summary>
internal sealed class InMemoryDeviceStore : IDeviceStore
{
    /// <summary>A device's public key and secret as a browser gives them, in the shape the notices check.</summary>
    public const string Key = "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";

    public const string Auth = "AAECAwQFBgcICQoLDA0ODw";

    private readonly Dictionary<AccountId, IReadOnlyList<PushDevice>> _held = [];

    public Task<IReadOnlyList<PushDevice>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            return Task.FromResult(_held.TryGetValue(account, out var devices) ? devices : []);
        }
    }

    public Task SaveAsync(AccountId account, IReadOnlyList<PushDevice> devices, CancellationToken cancellationToken = default)
    {
        lock (_held)
        {
            _held[account] = [.. devices];
        }

        return Task.CompletedTask;
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
