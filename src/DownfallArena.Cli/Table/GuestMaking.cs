using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The guests made at this host, counted by network address and in all (ADR 0207), so a script cannot make thousands:
/// <see cref="PerAddress" /> an hour from one address (an IPv6 address counts by its /64, <see cref="RequestAddress" />),
/// which a family or a classroom behind one address stays under, <see cref="PerNetwork" /> from all the /64s of one IPv6
/// /48 together, as <see cref="SignInAttempts" /> counts them, so one customer's many /64s are not many addresses (#1226),
/// and <see cref="PerHost" /> an hour from every address together, which one script rotating its addresses cannot get past. As the guests' feedback is counted (ADR 0189): in
/// this process's memory only, each address hashed with a salt the process draws, so what is held names nobody and a
/// restart forgets it. Thread-safe: a place is checked and taken under one gate, so two requests cannot both take the last.
/// </summary>
internal sealed class GuestMaking
{
    /// <summary>How many guests one address may make in a <see cref="Window" />.</summary>
    public const int PerAddress = 10;

    /// <summary>How many guests the /64s of one IPv6 /48 may make together in a <see cref="Window" />.</summary>
    public const int PerNetwork = 30;

    /// <summary>How many guests the host makes in a <see cref="Window" />, every address together.</summary>
    public const int PerHost = 300;

    /// <summary>The window the guests are counted over.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _made = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _all = new();

    /// <summary>
    /// Takes a place for one guest from <paramref name="address" />, and says how long to wait when there is none: the
    /// address, its IPv6 /48, or the host made its count in the hour before <paramref name="now" />. A request whose address is not
    /// known is counted in all only.
    /// </summary>
    public TimeSpan? Take(string? address, DateTimeOffset now)
    {
        var key = address is null ? null : Key(address);
        var wider = address is null ? null : RequestAddress.Network(address) is { } network ? Key("/48 " + network) : null;
        lock (_gate)
        {
            Forget(_all, now);
            foreach (var stale in _made.Where(pair => Forget(pair.Value, now) == 0).Select(pair => pair.Key).ToList())
            {
                _made.Remove(stale);
            }

            var own = key is null ? null : _made.GetValueOrDefault(key);
            if (own is { Count: >= PerAddress })
            {
                return own.Peek() + Window - now;
            }

            var near = wider is null ? null : _made.GetValueOrDefault(wider);
            if (near is { Count: >= PerNetwork })
            {
                return near.Peek() + Window - now;
            }

            if (_all.Count >= PerHost)
            {
                return _all.Peek() + Window - now;
            }

            if (key is not null && own is null)
            {
                _made[key] = own = new Queue<DateTimeOffset>();
            }

            if (wider is not null && near is null)
            {
                _made[wider] = near = new Queue<DateTimeOffset>();
            }

            own?.Enqueue(now);
            near?.Enqueue(now);
            _all.Enqueue(now);
            return null;
        }
    }

    // Lets go of the times an hour old, and says how many are left.
    private static int Forget(Queue<DateTimeOffset> times, DateTimeOffset now)
    {
        while (times.Count > 0 && times.Peek() <= now - Window)
        {
            times.Dequeue();
        }

        return times.Count;
    }

    private string Key(string address) => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(address)));
}
