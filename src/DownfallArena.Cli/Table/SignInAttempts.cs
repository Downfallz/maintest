using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The sign-ins posted from each network address (#1193), counted before a provider is asked: a provider's own budget
/// (Discord's thirty exchanges a minute) is shared by every player, and one address posting well-shaped proofs would
/// otherwise spend it for all. As the guests' feedback is counted (ADR 0189): in this process's memory only, each
/// address hashed with a salt the process draws, so what is held names nobody and a restart forgets it. Thread-safe:
/// the count is read and the attempt noted under one gate, so two requests cannot both pass on the same last place.
/// </summary>
internal sealed class SignInAttempts
{
    /// <summary>How many sign-ins one address may post in a <see cref="Window" />: a player needs one, a retry or two more.</summary>
    public const int PerAddress = 5;

    /// <summary>The window the attempts are counted over, the same as the provider's own budget.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _posted = new(StringComparer.Ordinal);
    private DateTimeOffset _swept = DateTimeOffset.MinValue;

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
                _posted[key] = times = new Queue<DateTimeOffset>();
            }

            while (times.Count > 0 && times.Peek() <= now - Window)
            {
                times.Dequeue();
            }

            if (times.Count >= PerAddress)
            {
                return times.Peek() + Window - now;
            }

            times.Enqueue(now);
            return null;
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
        foreach (var stale in _posted.Where(pair => pair.Value.All(at => at <= now - Window)).Select(pair => pair.Key).ToList())
        {
            _posted.Remove(stale);
        }
    }

    private string Key(string address) => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(address)));
}
