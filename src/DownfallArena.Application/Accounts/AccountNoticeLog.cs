using DownfallArena.Application.Accounts.Ports;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts;

/// <summary>
/// An account's notice log (ADR 0201): the news the bell keeps whatever the push does, the
/// <see cref="MostKept" /> latest of the last <see cref="KeptFor" />. A notice whose tag an entry already has replaces
/// it, newest and unread. Three processes write a log, so each change is read with its version, written over that
/// version only, and read again when another write came first.
/// </summary>
public sealed class AccountNoticeLog
{
    public const int MostKept = 100;

    public static readonly TimeSpan KeptFor = TimeSpan.FromDays(90);

    /// <summary>How many writes in a row a change may lose to others before it gives up.</summary>
    public const int Tries = 8;

    private readonly INoticeStore _store;
    private readonly TimeProvider _clock;

    public AccountNoticeLog(INoticeStore store, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _clock = clock;
    }

    /// <summary>The entries kept now, newest first: an entry past <see cref="KeptFor" /> is not listed, written or not.</summary>
    public async Task<IReadOnlyList<NoticeEntry>> ListAsync(AccountId account, CancellationToken cancellationToken = default)
    {
        var read = await _store.ReadAsync(account, cancellationToken);
        return Kept(read.Entries, _clock.GetUtcNow());
    }

    /// <summary>Keeps the notice as the newest entry, unread; an entry of the same tag goes. The entry kept.</summary>
    public async Task<NoticeEntry> AddAsync(AccountId account, Notice notice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        NoticeEntry? added = null;
        await ChangeAsync(account, entries =>
        {
            var now = _clock.GetUtcNow();
            // The id grows with the log: past the newest one, and the time in milliseconds otherwise, so a log emptied
            // by time does not hand an old page's "read up to" an id it would cover.
            var id = Math.Max(entries.Count == 0 ? 0 : entries.Max(entry => entry.Id) + 1, now.ToUnixTimeMilliseconds());
            added = new NoticeEntry(id, notice.Kind, notice.Title, notice.Body, notice.Url, notice.Tag, now, Read: false);
            return [added, .. entries.Where(entry => !string.Equals(entry.Tag, notice.Tag, StringComparison.Ordinal))];
        }, cancellationToken);
        return added!;
    }

    /// <summary>Marks read every entry up to that id, that one included. How many were unread and are read now.</summary>
    public async Task<int> MarkReadAsync(AccountId account, long upTo, CancellationToken cancellationToken = default)
    {
        var marked = 0;
        await ChangeAsync(account, entries =>
        {
            marked = entries.Count(entry => !entry.Read && entry.Id <= upTo);
            return marked == 0 ? null : [.. entries.Select(entry => entry.Id <= upTo ? entry with { Read = true } : entry)];
        }, cancellationToken);
        return marked;
    }

    /// <summary>
    /// The sweep of the hourly job (#1118): every log that holds an entry past <see cref="KeptFor" /> is written without
    /// it, and a log left with none goes, so an account nobody told since keeps nothing past its time at rest, the
    /// other players' names with it. A log with nothing past its time is not written. How many logs it wrote.
    /// </summary>
    public async Task<int> EraseExpiredAsync(CancellationToken cancellationToken = default)
    {
        var erased = 0;
        foreach (var account in await _store.AccountsAsync(cancellationToken))
        {
            var read = await _store.ReadAsync(account, cancellationToken);
            var kept = Kept(read.Entries, _clock.GetUtcNow());
            // Refused, another write came first, and every write cuts the log to what is kept: nothing is left to do.
            if (kept.Count < read.Entries.Count && await _store.WriteAsync(account, kept, read.Version, cancellationToken))
            {
                erased++;
            }
        }

        return erased;
    }

    /// <summary>The log goes with the account.</summary>
    public Task ForgetAsync(AccountId account, CancellationToken cancellationToken = default) => _store.ForgetAsync(account, cancellationToken);

    /// <summary>
    /// Reads the log, changes it, cuts it to what is kept, and writes it over the version read, reading again while
    /// another write comes first. A change that answers none writes nothing. Every try lost, it throws.
    /// </summary>
    private async Task ChangeAsync(AccountId account, Func<IReadOnlyList<NoticeEntry>, IReadOnlyList<NoticeEntry>?> change, CancellationToken cancellationToken)
    {
        for (var tried = 0; tried < Tries; tried++)
        {
            var read = await _store.ReadAsync(account, cancellationToken);
            var changed = change(Kept(read.Entries, _clock.GetUtcNow()));
            if (changed is null)
            {
                return;
            }

            if (await _store.WriteAsync(account, Kept(changed, _clock.GetUtcNow()), read.Version, cancellationToken))
            {
                return;
            }
        }

        throw new IOException($"The notice log of account {account} lost {Tries} writes in a row to others.");
    }

    private static List<NoticeEntry> Kept(IEnumerable<NoticeEntry> entries, DateTimeOffset now) =>
        [.. entries.Where(entry => entry.At > now - KeptFor).OrderByDescending(entry => entry.Id).Take(MostKept)];
}
