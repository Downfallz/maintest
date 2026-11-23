using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The sign-ins posted from each network address (#1193), counted before a provider is asked: a provider's own budget
/// (Discord's thirty exchanges a minute) is shared by every player, and one address posting well-shaped proofs would
/// otherwise spend it for all. A sign-in the provider vouches for is given back, so a room of players behind one address
/// is not held to five a minute. An IPv6 address is counted by its /48, which one customer of an ISP holds, not by the
/// /64 <see cref="RequestAddress" /> gives. As the guests' feedback is counted (ADR 0189): in this process's memory
/// only, each address hashed with a salt the process draws, so what is held names nobody and a restart forgets it.
/// Thread-safe: the count is read and the attempt noted under one gate, so two requests cannot both take the last place.
/// </summary>
internal sealed class SignInAttempts
{
    /// <summary>How many sign-ins one address may post in a <see cref="Window" /> that its provider does not vouch for.</summary>
    public const int PerAddress = 5;

    /// <summary>The window the attempts are counted over, the same as the provider's own budget.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _posted = new(StringComparer.Ordinal);
    private DateTimeOffset _swept = DateTimeOffset.MinValue;

    /// <summary>How many addresses the count holds: what the sweep lets go of.</summary>
    internal int Held
    {
        get
        {
            lock (_gate)
            {
                return _posted.Count;
            }
        }
    }

    /// <summary>
    /// Notes one attempt from the address and lets it through, or refuses it, noting nothing, with how long until the
    /// address's oldest attempt leaves the window. A refused attempt is not counted: the wait it is told is true.
    /// </summary>
    public TimeSpan? Take(string address, DateTimeOffset now)
    {
        var key = Key(address);
        lock (_gate)
        {
            Sweep(now);
            if (!_posted.TryGetValue(key, out var times))
            {
                _posted[key] = times = [];
            }

            times.RemoveAll(at => at <= now - Window);
            if (times.Count >= PerAddress)
            {
                return times.Min() + Window - now;
            }

            times.Add(now);
            return null;
        }
    }

    /// <summary>Gives back the attempt taken at <paramref name="at" />: the provider vouched for it, so it spent nobody's budget in vain.</summary>
    public void GiveBack(string address, DateTimeOffset at)
    {
        var key = Key(address);
        lock (_gate)
        {
            if (_posted.TryGetValue(key, out var times))
            {
                times.Remove(at);
            }
        }
    }

    // The addresses whose attempts are all out of the window are let go of, once a window at most, so a sweep is not
    // paid on every request of many addresses.
    private void Sweep(DateTimeOffset now)
    {
        if (now - _swept < Window)
        {
            return;
        }

        _swept = now;
        foreach (var stale in _posted.Where(pair => pair.Value.TrueForAll(at => at <= now - Window)).Select(pair => pair.Key).ToList())
        {
            _posted.Remove(stale);
        }
    }

    private string Key(string address) => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(Counted(address))));

    /// <summary>The address as it is counted: an IPv6 network by its /48, any other address as it is.</summary>
    private static string Counted(string address)
    {
        var network = IPNetwork.TryParse(address, out var parsed) ? parsed : IPAddress.TryParse(address, out var one) ? new IPNetwork(one, one.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32) : (IPNetwork?)null;
        if (network is not { BaseAddress.AddressFamily: AddressFamily.InterNetworkV6 } v6 || v6.PrefixLength < 48)
        {
            return address;
        }

        var bytes = v6.BaseAddress.GetAddressBytes();
        Array.Clear(bytes, 6, 10);
        return $"{new IPNetwork(new IPAddress(bytes), 48)}";
    }
}
