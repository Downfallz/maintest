namespace DownfallArena.Cli.Table;

/// <summary>
/// What each seat of a table was last shown of its game, and when (ADR 0194): the mark the recap reads to tell a
/// player coming back what they have not seen, kept at the host so it follows them from one device to another.
/// </summary>
/// <remarks>
/// Kept beside the table's record when the table is recorded (<c>seen-player1.json</c>, <c>seen-player2.json</c>),
/// never in it: the mark moves far more often than decisions do, and must not race one for the record's tag. Only a
/// larger sequence is kept, written only over the version read, so two devices and two processes keep the furthest
/// mark. It is read from the record on every read, so another process's mark is never missed. A table nobody records
/// keeps the marks for as long as it is held.
/// </remarks>
internal sealed class TableVisits(TimeProvider clock, IVisitKeeper? keeper = null)
{
    // A write refused because another process wrote first is read again and tried again, this many times at most.
    private const int Attempts = 3;

    private readonly Lock _held = new();
    private readonly Dictionary<string, LastVisit> _visits = [];

    /// <summary>The seat's last visit under <paramref name="holder" />, or none before its page said what it was shown.</summary>
    public async Task<LastVisit?> LastAsync(string seat, string holder, CancellationToken cancellationToken = default)
    {
        LastVisit? visit;
        if (keeper is null)
        {
            lock (_held)
            {
                visit = _visits.GetValueOrDefault(seat);
            }
        }
        else
        {
            visit = (await keeper.VisitAsync(seat, cancellationToken)).Visit;
        }

        return visit?.Holder == holder ? visit : null;
    }

    /// <summary>
    /// The seat, held under <paramref name="holder" />, was shown up to <paramref name="sequence" />: kept, with the
    /// time now, when it is further than the mark kept, or when that mark is another holder's. The mark kept
    /// afterwards; none when every retry lost the race to another process, or the record went away meanwhile.
    /// </summary>
    public async Task<LastVisit?> ShownAsync(string seat, string holder, long sequence, CancellationToken cancellationToken = default)
    {
        if (keeper is null)
        {
            lock (_held)
            {
                if (_visits.GetValueOrDefault(seat) is { } held && held.Holder == holder && held.Sequence >= sequence)
                {
                    return held;
                }

                return _visits[seat] = new LastVisit(sequence, clock.GetUtcNow(), holder);
            }
        }

        var existed = false;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var (kept, tag) = await keeper.VisitAsync(seat, cancellationToken);

            // There was a mark and now there is none: the table's run was deleted meanwhile, and writing would bring a
            // file of it back.
            if (existed && tag is null)
            {
                return null;
            }

            existed |= tag is not null;
            if (kept is not null && kept.Holder == holder && kept.Sequence >= sequence)
            {
                return kept;
            }

            var visit = new LastVisit(sequence, clock.GetUtcNow(), holder);
            if (await keeper.WriteVisitIfAsync(seat, visit, tag, cancellationToken) is not null)
            {
                return visit;
            }
        }

        return null;
    }
}
