using System.Collections.Concurrent;

namespace DownfallArena.Infrastructure.Accounts;

/// <summary>
/// The text a store last read of each document, with the version it was read at, so a conditional write over that
/// version writes back the fields this revision does not know (#1211) without reading the document again (#1232). A
/// version names one text, a blob's tag or a file's hash, so the text kept is the one a second read would return. A
/// write it cannot answer, from a read another store made, one it forgot or one too old, reads the document again,
/// as before.
/// </summary>
/// <remarks>
/// A write follows its read within the same request, so a text is kept for <see cref="Lifetime" /> only. That bounds
/// what an account erased by another process leaves here, its push keys among it (ADR 0203): such a text is never
/// given to a write once old, and is let go at a later read or write of the store, within two lifetimes. A store
/// that is not used again keeps it until its process ends: the reads that save nothing (a push's devices) keep no text.
/// </remarks>
/// <typeparam name="TKey">What names a document in its store: an account, a day.</typeparam>
internal sealed class TextsRead<TKey>
    where TKey : notnull
{
    /// <summary>How long a text is kept: far longer than a read and the write after it, far shorter than a session.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    /// <summary>A few hundred documents at most: past it the store forgets them all, and its next writes read again.</summary>
    private const int Capacity = 256;

    private readonly ConcurrentDictionary<TKey, (string Version, string Text, DateTimeOffset Kept)> _texts = new();
    private readonly TimeProvider _time;
    private long _swept;

    public TextsRead(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _swept = _time.GetUtcNow().UtcTicks;
    }

    /// <summary>How many texts are kept now, what an erasure in another process may leave here.</summary>
    public int Count => _texts.Count;

    /// <summary>Keeps the text read at that version; a document that is not there forgets what was kept of it.</summary>
    public void Remember(TKey key, string? text, string? version)
    {
        var now = Sweep();
        if (text is null || version is null)
        {
            _texts.TryRemove(key, out _);
            return;
        }

        if (!_texts.ContainsKey(key) && _texts.Count >= Capacity)
        {
            _texts.Clear();
        }

        _texts[key] = (version, text, now);
    }

    /// <summary>
    /// Lets go of what was kept of a document this store deletes or empties. Called before and after the deletion, so a
    /// read whose answer is kept between the two keeps nothing either.
    /// </summary>
    public void Forget(TKey key) => _texts.TryRemove(key, out _);

    /// <summary>Lets go of what was kept of every document <paramref name="which" /> names, those the store deleted.</summary>
    public void Forget(Func<TKey, bool> which)
    {
        ArgumentNullException.ThrowIfNull(which);
        foreach (var key in _texts.Keys.Where(which))
        {
            _texts.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// The text the document holds at that version, taken from what was kept or read again. When it was read again and
    /// the document has moved past that version, none, with false, so the write is refused without being tried; a kept
    /// text cannot tell, and the conditional write is refused instead. What was kept is given to one write only: the
    /// version it names is gone once that write is made.
    /// </summary>
    public async Task<(bool Current, string? Text)> AtAsync(
        TKey key,
        string version,
        Func<CancellationToken, Task<(string? Text, string? Version)>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        var now = Sweep();
        if (_texts.TryGetValue(key, out var kept)
            && _texts.TryRemove(KeyValuePair.Create(key, kept))
            && string.Equals(kept.Version, version, StringComparison.Ordinal)
            && now - kept.Kept < Lifetime)
        {
            return (true, kept.Text);
        }

        var (text, current) = await read(cancellationToken);
        return string.Equals(current, version, StringComparison.Ordinal) ? (true, text) : (false, null);
    }

    /// <summary>Lets go of the texts kept past their lifetime, at most once a lifetime; the time now.</summary>
    private DateTimeOffset Sweep()
    {
        var now = _time.GetUtcNow();
        var swept = Interlocked.Read(ref _swept);
        if (now.UtcTicks - swept >= Lifetime.Ticks && Interlocked.CompareExchange(ref _swept, now.UtcTicks, swept) == swept)
        {
            foreach (var (key, kept) in _texts)
            {
                if (now - kept.Kept >= Lifetime)
                {
                    _texts.TryRemove(KeyValuePair.Create(key, kept));
                }
            }
        }

        return now;
    }
}
