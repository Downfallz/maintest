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
/// A write follows its read within the same request, so a text is kept for <see cref="LifetimeSeconds" /> seconds only. That bounds
/// what an account erased by another process leaves here, its push keys among it (ADR 0203): such a text is never
/// given to a write once old, and is let go within two lifetimes, by a sweep that runs every lifetime while a text is
/// kept, whether or not the store is used again (#1260). A store that keeps nothing holds no sweep; the reads that save
/// nothing (a push's devices) keep no text.
/// </remarks>
/// <typeparam name="TKey">What names a document in its store: an account, a day.</typeparam>
internal sealed class TextsRead<TKey>
    where TKey : notnull
{
    /// <summary>How long a text is kept, in seconds: far longer than a read and the write after it, far shorter than a session.</summary>
    public const int LifetimeSeconds = 30;

    /// <summary>A few hundred documents at most: past it the store forgets them all, and its next writes read again.</summary>
    private const int Capacity = 256;

    private static TimeSpan Lifetime => TimeSpan.FromSeconds(LifetimeSeconds);

    private readonly ConcurrentDictionary<TKey, (string Version, string Text, DateTimeOffset Kept)> _texts = new();
    private readonly TimeProvider _time;
    private readonly Lock _sweeping = new();

    // Let go once nothing is kept, so a store that keeps nothing holds no timer.
    private ITimer? _sweep;

    public TextsRead(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>How many texts are kept now, what an erasure in another process may leave here.</summary>
    public int Count => _texts.Count;

    /// <summary>Keeps the text read at that version; a document that is not there forgets what was kept of it.</summary>
    public void Remember(TKey key, string? text, string? version)
    {
        var now = _time.GetUtcNow();
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
        lock (_sweeping)
        {
            _sweep ??= _time.CreateTimer(_ => SweepUnasked(), state: null, Lifetime, Lifetime);
        }
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

    /// <summary>Whether a sweep is due: held only while a text is kept.</summary>
    public bool Sweeping
    {
        get
        {
            lock (_sweeping)
            {
                return _sweep is not null;
            }
        }
    }

    /// <summary>
    /// Writes the document over that version only (ADR 0201): <paramref name="document" /> made from the text read at
    /// it, so the fields this revision does not know are written back (#1211), or, for none, the document deleted and
    /// what was kept of it forgotten, before and after. False when another write came first.
    /// </summary>
    public async Task<bool> WriteAsync(
        TKey key,
        string? version,
        Func<string?, string>? document,
        Func<CancellationToken, Task<(string? Text, string? Version)>> read,
        Func<string?, CancellationToken, Task<bool>> write,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (document is null)
        {
            return await ForgetAroundAsync(key, () => write(null, cancellationToken));
        }

        string? before = null;
        if (version is not null)
        {
            (var current, before) = await AtAsync(key, version, read, cancellationToken);
            if (!current)
            {
                return false;
            }
        }

        return await write(document(before), cancellationToken);
    }

    /// <summary>Deletes the document, forgetting what was kept of it before and after, its push keys among it (ADR 0203).</summary>
    public Task ForgetAsync(TKey key, Func<Task> delete)
    {
        ArgumentNullException.ThrowIfNull(delete);
        return ForgetAroundAsync(key, async () =>
        {
            await delete();
            return true;
        });
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
        var now = _time.GetUtcNow();
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

    /// <summary>Deletes the documents <paramref name="which" /> names, forgetting what was kept of them before and after.</summary>
    public async Task ForgetAsync(Func<TKey, bool> which, Func<Task> delete)
    {
        ArgumentNullException.ThrowIfNull(delete);
        Forget(which);
        await delete();
        Forget(which);
    }

    private async Task<bool> ForgetAroundAsync(TKey key, Func<Task<bool>> deletion)
    {
        Forget(key);
        var done = await deletion();
        Forget(key);
        return done;
    }

    /// <summary>
    /// The sweep a timer runs every lifetime while a text is kept: the texts past their lifetime let go, then the timer
    /// itself once nothing is kept. A text kept meanwhile finds the timer still there, or starts another.
    /// </summary>
    private void SweepUnasked()
    {
        lock (_sweeping)
        {
            LetGoOld(_time.GetUtcNow());
            if (_texts.IsEmpty && _sweep is { } sweep)
            {
                sweep.Dispose();
                _sweep = null;
            }
        }
    }

    private void LetGoOld(DateTimeOffset now)
    {
        foreach (var (key, kept) in _texts)
        {
            if (now - kept.Kept >= Lifetime)
            {
                _texts.TryRemove(KeyValuePair.Create(key, kept));
            }
        }
    }
}
