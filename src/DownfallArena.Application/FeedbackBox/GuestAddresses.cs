using System.Security.Cryptography;
using System.Text;

namespace DownfallArena.Application.FeedbackBox;

/// <summary>
/// The guests' feedback counted by network address (ADR 0189, the third guard): a guest opens a table, and so a new
/// seat, in one request, so the seat alone bounds nothing. Held in this process's memory only, each address hashed with
/// a salt the process draws, so what is held names nobody and a restart forgets it. Thread-safe.
/// </summary>
public sealed class GuestAddresses
{
    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _sent = new(StringComparer.Ordinal);

    /// <summary>How much feedback was sent from the address in the day before <paramref name="now" />.</summary>
    public int SentSince(string address, DateTimeOffset now)
    {
        var key = Key(address);
        lock (_gate)
        {
            return _sent.TryGetValue(key, out var times) ? times.Count(at => at > now - SendFeedback.Day) : 0;
        }
    }

    /// <summary>Counts feedback sent from the address, and lets go of what is a day old.</summary>
    public void Note(string address, DateTimeOffset now)
    {
        var key = Key(address);
        lock (_gate)
        {
            foreach (var stale in _sent.Where(pair => pair.Value.TrueForAll(at => at <= now - SendFeedback.Day)).Select(pair => pair.Key).ToList())
            {
                _sent.Remove(stale);
            }

            if (!_sent.TryGetValue(key, out var times))
            {
                _sent[key] = times = [];
            }

            times.RemoveAll(at => at <= now - SendFeedback.Day);
            times.Add(now);
        }
    }

    private string Key(string address) => Convert.ToHexStringLower(HMACSHA256.HashData(_salt, Encoding.UTF8.GetBytes(address)));
}
