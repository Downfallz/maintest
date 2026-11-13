namespace DownfallArena.Cli.Table;

/// <summary>
/// The reactions two players send each other during a match (ADR 0173): a fixed set of short messages, never free
/// text, so there is nothing to moderate and nothing one player can make the other read. A seat sends one at most
/// every <see cref="Cooldown" />, and the table keeps the last <see cref="Kept" /> of them, both seats', which is what
/// each seat's view serves.
/// </summary>
/// <remarks>
/// Kept beside the table's record when the table is recorded (<c>reactions.json</c>), written only over the version
/// read, so a game played over days shows the reaction sent while the other player was away, after a deploy too, and
/// two processes writing at once cannot lose one. A table nobody records keeps them for as long as it is held.
/// </remarks>
internal sealed class TableReactions(TimeProvider clock, IReactionKeeper? keeper = null)
{
    /// <summary>Every reaction a seat may send, by the name the page shows a label for; the order is the menu's.</summary>
    public static IReadOnlyList<string> Catalogue { get; } =
        ["hello", "good-luck", "well-played", "nice-move", "oops", "thinking", "thanks", "good-game"];

    /// <summary>How long a seat waits between two reactions: one tap is a reaction, a burst of them is noise.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    /// <summary>How many reactions the table keeps, both seats together: enough for a page away for a while to show the last one.</summary>
    public const int Kept = 8;

    // A write refused because another process wrote first is read again and tried again, this many times at most.
    private const int Attempts = 3;

    // Reads and writes take turns, one after another, without a handle to dispose: each waits for the one before.
    private readonly Lock _order = new();
    private Task _last = Task.CompletedTask;
    private IReadOnlyList<TableReaction>? _items;
    private string? _tag;

    /// <summary>The reactions kept, oldest first; read from the record the first time they are asked for.</summary>
    public async Task<IReadOnlyList<TableReaction>> RecentAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _items) is { } items)
        {
            return items;
        }

        return await InTurn(() => LoadedAsync(cancellationToken));
    }

    /// <summary>Sends <paramref name="reaction" /> for <paramref name="seat" />, or says why it was not sent.</summary>
    public async Task<ReactionSent> SendAsync(string seat, string? reaction, CancellationToken cancellationToken = default)
    {
        if (reaction is null || !Catalogue.Contains(reaction, StringComparer.Ordinal))
        {
            return ReactionSent.Unknown;
        }

        return await InTurn(() => SendInTurnAsync(seat, reaction, cancellationToken));
    }

    private async Task<ReactionSent> SendInTurnAsync(string seat, string reaction, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var items = await LoadedAsync(cancellationToken);
            var now = clock.GetUtcNow();
            if (items.LastOrDefault(one => one.Seat == seat) is { } last && now - last.At < Cooldown)
            {
                return ReactionSent.TooSoon(Cooldown - (now - last.At));
            }

            var sent = new TableReaction((items.Count == 0 ? 0 : items[^1].Sequence) + 1, seat, reaction, now);
            IReadOnlyList<TableReaction> next = [.. items.Append(sent).TakeLast(Kept)];
            if (keeper is null)
            {
                Volatile.Write(ref _items, next);
                return ReactionSent.Of(sent);
            }

            if (await keeper.WriteReactionsIfAsync(next, _tag, cancellationToken) is { } tag)
            {
                _tag = tag;
                Volatile.Write(ref _items, next);
                return ReactionSent.Of(sent);
            }

            // Another process wrote first: what it wrote is read, and this one goes after it.
            Volatile.Write(ref _items, null);
            if (attempt == Attempts)
            {
                return ReactionSent.Busy;
            }
        }
    }

    private Task<T> InTurn<T>(Func<Task<T>> work)
    {
        lock (_order)
        {
            var turn = _last.ContinueWith(_ => work(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
            _last = turn;
            return turn;
        }
    }

    /// <summary>
    /// Forgets what was read, so the next view reads the record again: what a process does when another one says it
    /// moved this table, and a reaction it wrote is one of the things that may have moved.
    /// </summary>
    public void Stale()
    {
        if (keeper is not null)
        {
            Volatile.Write(ref _items, null);
        }
    }

    // In turn.
    private async Task<IReadOnlyList<TableReaction>> LoadedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _items) is { } items)
        {
            return items;
        }

        if (keeper is null)
        {
            Volatile.Write(ref _items, []);
            return [];
        }

        var (read, tag) = await keeper.ReactionsAsync(cancellationToken);
        IReadOnlyList<TableReaction> kept = [.. read.Where(one => Catalogue.Contains(one.Reaction, StringComparer.Ordinal)).OrderBy(one => one.Sequence).TakeLast(Kept)];
        _tag = tag;
        Volatile.Write(ref _items, kept);
        return kept;
    }
}
