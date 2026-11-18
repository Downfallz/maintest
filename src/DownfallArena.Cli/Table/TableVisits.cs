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

    // The marks on their way into the record, which the operator's delete waits for (#849). Each is counted before its
    // first read, so a delete never misses one that has started.
    private readonly HashSet<Task> _writing = [];

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
    public async Task<VisitShown> ShownAsync(string seat, string holder, long sequence, CancellationToken cancellationToken = default)
    {
        if (keeper is null)
        {
            return VisitShown.Of(ShownHere(seat, holder, sequence));
        }

        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_held)
        {
            _writing.Add(writing.Task);
        }

        try
        {
            return await KeptAsync(seat, holder, sequence, cancellationToken);
        }
        finally
        {
            lock (_held)
            {
                _writing.Remove(writing.Task);
            }

            writing.TrySetResult();
        }
    }

    private async Task<VisitShown> KeptAsync(string seat, string holder, long sequence, CancellationToken cancellationToken)
    {
        var store = keeper!;
        var existed = false;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var (kept, tag) = await store.VisitAsync(seat, cancellationToken);
            if (await RefusedBeforeWriteAsync(existed, tag, cancellationToken) is { } refused)
            {
                return refused;
            }

            existed |= tag is not null;
            if (kept is not null && kept.Holder == holder && kept.Sequence >= sequence)
            {
                return VisitShown.Of(kept);
            }

            var visit = new LastVisit(sequence, clock.GetUtcNow(), holder);
            if (await store.WriteVisitIfAsync(seat, visit, tag, cancellationToken) is { } written)
            {
                return await WrittenAsync(seat, visit, first: tag is null, written, cancellationToken);
            }
        }

        return VisitShown.Busy;
    }

    // Why the mark read under this tag must not be written over; none when it may be.
    private async Task<VisitShown?> RefusedBeforeWriteAsync(bool existed, string? tag, CancellationToken cancellationToken)
    {
        // There was a mark and now there is none: the table's run was deleted meanwhile, and writing would bring a
        // file of it back.
        if (existed && tag is null)
        {
            return VisitShown.Gone;
        }

        // No mark yet, and no record either: a write would create the file under a deleted run (#789). Busy, not gone,
        // when this process never saw the record stored (#850).
        return tag is null && await keeper!.RecordedAsync(cancellationToken) is not RecordPresence.Kept and var missing
            ? Refused(missing)
            : null;
    }

    // The mark written, or taken back when it was the seat's first and the record has gone since.
    private async Task<VisitShown> WrittenAsync(string seat, LastVisit visit, bool first, string written, CancellationToken cancellationToken)
    {
        // Same check as in TableReactions, after the first write: a record still there means the delete
        // has not listed the run's files yet (#818).
        if (first && await StillRecordedAsync(cancellationToken) is not RecordPresence.Kept and var gone)
        {
            await TakeBackAsync(seat, written, cancellationToken);
            return Refused(gone);
        }

        return VisitShown.Of(visit);
    }

    /// <summary>
    /// Completes once every mark already on its way into the record has ended, however it ended (#849): what the
    /// operator's delete waits for, so a mark lands before the run's files are listed, not after.
    /// </summary>
    public Task SettledAsync()
    {
        Task[] writing;
        lock (_held)
        {
            writing = [.. _writing];
        }

        return Task.WhenAll(writing);
    }

    // A failed read of the record is no evidence of a delete, and the write has landed: it is kept.
    private async Task<RecordPresence> StillRecordedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await keeper!.RecordedAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return RecordPresence.Kept;
        }
    }

    // A record the operator deleted is gone for good; one this process never saw stored may be written yet (#850).
    private static VisitShown Refused(RecordPresence missing) => missing == RecordPresence.Gone ? VisitShown.Gone : VisitShown.Busy;

    // The mark written is deleted. When another process wrote over it meanwhile, what it holds now is deleted instead (#818).
    private async Task TakeBackAsync(string seat, string tag, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= Attempts && !await keeper!.ForgetVisitIfAsync(seat, tag, cancellationToken); attempt++)
        {
            if ((await keeper.VisitAsync(seat, cancellationToken)).Tag is not { } current)
            {
                return;
            }

            tag = current;
        }
    }

    // A table nobody records: the mark is kept in memory, for as long as the table is held.
    private LastVisit ShownHere(string seat, string holder, long sequence)
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
}
