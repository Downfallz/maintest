using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The sign-ins posted from each network address (#1193), checked before a provider is asked and settled once it
/// answered. A provider's own budget (Discord's thirty exchanges a minute) is shared by every player, and one address
/// posting proofs would otherwise spend it for all. What an address may do in a <see cref="Window" />:
/// <list type="bullet">
/// <item><see cref="PerAddress" /> sign-ins its provider did not vouch for (forged, refused, or one person signing in
/// again), and an IPv6 /48 <see cref="PerNetwork" /> over its /64s, so one customer's many /64s are not many
/// addresses while one noisy /64 leaves its neighbours theirs;</item>
/// <item><see cref="VouchedPerAddress" /> different people the provider vouched for, so a room behind one address
/// signs in and one account looping does not spend the budget;</item>
/// <item><see cref="AtOnce" /> sign-ins being checked at the same time.</item>
/// </list>
/// A sign-in the provider could not check (out of service) is not counted: it cost the budget nothing. As the guests'
/// feedback is counted (ADR 0189): in this process's memory only, each address and person hashed with a salt the
/// process draws, so what is held names nobody and a restart forgets it. Thread-safe: an address is checked and its
/// place taken under one gate, so two requests cannot both take the last place.
/// </summary>
internal sealed class SignInAttempts
{
    /// <summary>How many sign-ins its provider did not vouch for one address may post in a <see cref="Window" />.</summary>
    public const int PerAddress = 5;

    /// <summary>The same, for all the /64s of one IPv6 /48 together: Discord's own budget.</summary>
    public const int PerNetwork = 30;

    /// <summary>How many different people the provider may vouch for from one address in a <see cref="Window" />.</summary>
    public const int VouchedPerAddress = 20;

    /// <summary>How many sign-ins of one address may be checked at the same time; past it the address waits a second.</summary>
    public const int AtOnce = 8;

    /// <summary>The window the attempts are counted over, the same as the provider's own budget.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan Moment = TimeSpan.FromSeconds(1);

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SignInTally> _held = new(StringComparer.Ordinal);
    private DateTimeOffset _swept = DateTimeOffset.MinValue;

    /// <summary>How a checked sign-in ended, as the count reads it.</summary>
    public enum Outcome
    {
        /// <summary>The provider vouched for a person: counted once per person, apart from the refusals.</summary>
        Vouched,

        /// <summary>The provider refused the proof: counted against the address and its network.</summary>
        Refused,

        /// <summary>The provider could not be asked: not counted.</summary>
        Unchecked,
    }

    /// <summary>How many addresses and networks the count holds: what the sweep lets go of.</summary>
    internal int Held
    {
        get
        {
            lock (_gate)
            {
                return _held.Count;
            }
        }
    }

    /// <summary>
    /// Lets one sign-in of the address be checked, its place taken until <see cref="Settle" />, or refuses it, taking
    /// nothing, with how long until a place is free.
    /// </summary>
    public TimeSpan? Admit(string address, DateTimeOffset now)
    {
        var (own, network) = Keys(address);
        lock (_gate)
        {
            Sweep(now);
            var tally = TallyOf(own, now);
            var wider = network is null ? null : TallyOf(network, now);
            if (tally.Refused.Count >= PerAddress)
            {
                return tally.Refused.Min() + Window - now;
            }

            if (wider is { Refused.Count: >= PerNetwork })
            {
                return wider.Refused.Min() + Window - now;
            }

            if (tally.Vouched.Count >= VouchedPerAddress)
            {
                return tally.Vouched.Min(one => one.At) + Window - now;
            }

            if (tally.Checking >= AtOnce)
            {
                return Moment;
            }

            tally.Checking++;
            return null;
        }
    }

    /// <summary>Gives back the place <see cref="Admit" /> took, and counts how the sign-in ended; <paramref name="person" /> is who was vouched for.</summary>
    public void Settle(string address, DateTimeOffset now, Outcome outcome, string? person = null)
    {
        var (own, network) = Keys(address);
        var who = person is null ? null : Hash(person);
        lock (_gate)
        {
            var tally = TallyOf(own, now);
            tally.Checking = Math.Max(0, tally.Checking - 1);
            if (outcome == Outcome.Unchecked)
            {
                return;
            }

            if (outcome == Outcome.Vouched && who is not null && !tally.Vouched.Exists(one => one.Who == who))
            {
                tally.Vouched.Add((now, who));
                return;
            }

            tally.Refused.Add(now);
            if (network is not null)
            {
                TallyOf(network, now).Refused.Add(now);
            }
        }
    }

    // The tally of a key, its old entries let go of.
    private SignInTally TallyOf(string key, DateTimeOffset now)
    {
        if (!_held.TryGetValue(key, out var tally))
        {
            _held[key] = tally = new SignInTally();
        }

        tally.Forget(now - Window);
        return tally;
    }

    // The tallies with nothing in the window and nothing being checked are let go of, once a window at most, so a sweep
    // is not paid on every request of many addresses.
    private void Sweep(DateTimeOffset now)
    {
        if (now - _swept < Window)
        {
            return;
        }

        _swept = now;
        foreach (var (key, tally) in _held.ToList())
        {
            tally.Forget(now - Window);
            if (tally.Idle)
            {
                _held.Remove(key);
            }
        }
    }

    // The address's own key, and its IPv6 /48's when it has one.
    private (string Own, string? Network) Keys(string address) =>
        (Hash(address), Network(address) is { } network ? Hash("/48 " + network) : null);

    private string Hash(string text) => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(text)));

    /// <summary>The /48 an IPv6 address or network is in; null for any other address.</summary>
    private static string? Network(string address)
    {
        var network = IPNetwork.TryParse(address, out var parsed) ? parsed : IPAddress.TryParse(address, out var one) ? new IPNetwork(one, one.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32) : (IPNetwork?)null;
        if (network is not { BaseAddress.AddressFamily: AddressFamily.InterNetworkV6 } v6 || v6.PrefixLength < 48)
        {
            return null;
        }

        var bytes = v6.BaseAddress.GetAddressBytes();
        Array.Clear(bytes, 6, 10);
        return $"{new IPNetwork(new IPAddress(bytes), 48)}";
    }
}
