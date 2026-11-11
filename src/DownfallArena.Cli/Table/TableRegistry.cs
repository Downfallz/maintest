using DownfallArena.Application.Tables.Ports;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The tables this host holds, and the way to every other one (ADR 0081, ADR 0161): a cache a table is picked up
/// into from its record and journal when a request names it, by a key that names its table (<see cref="TableKeys" />)
/// or a code the index of the tables names. What lists, counts and finds is the index; what plays is the table
/// held here.
/// </summary>
/// <remarks>
/// <para>
/// A table is held from when it is opened or picked up until it is let go of: finished tables after an hour
/// without a reader, and a table that never began -- nobody came to it, or its second player never did -- closed
/// after <see cref="NeverBegunFor" /> without a request, or a lobby anyone may open tables from would fill the
/// host for good (ADR 0115). A game that has begun stays open until it ends or the operator closes it: closing a
/// browser must not end the game it comes back to. The sweep runs on requests, at most once a minute outside the
/// admin panel, over the tables held here and over the open tables of the index nobody holds.
/// </para>
/// <para>
/// Keys minted before a key named its table (4.3a) name none; the host is told them once, as it starts, from the
/// open records (<see cref="Remember" />), and they work for the life of those tables.
/// </para>
/// </remarks>
internal sealed class TableRegistry : IDisposable
{
    /// <summary>How long a finished table stays readable after the last request: the end screen, the session page.</summary>
    public static readonly TimeSpan FinishedFor = TimeSpan.FromHours(1);

    /// <summary>
    /// How long a table that never began is kept without a request: an evening and a night's sleep, the time
    /// an invitation reasonably waits. A game that began is never swept unfinished.
    /// </summary>
    public static readonly TimeSpan NeverBegunFor = TimeSpan.FromHours(20);

    /// <summary>How often a request other than the admin panel's is allowed to sweep.</summary>
    public static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many tables may be under way at once, across the index. The admin panel is behind the operator's login,
    /// so this bounds a mistake rather than an attack: a table holds its match in memory and its bots think on a
    /// host's processor, and a host with hundreds of them is a host that stopped answering. A table waiting for its
    /// players (ADR 0092) counts: it plays nothing yet, but it will the moment they arrive, and refusing them then
    /// would strand two people who have just sat down, while refusing the operator now is one line on their panel.
    /// </summary>
    public const int MostUnderWay = 16;

    /// <summary>How many open tables a listing reads off the index: far more than the cap, which bounds them.</summary>
    internal const int Listing = 1000;

    /// <summary>How long a table that could not be picked up is not tried again: a lobby polled every ten seconds must not replay it at each poll.</summary>
    public static readonly TimeSpan RefusedFor = TimeSpan.FromMinutes(1);

    /// <summary>How many codes are drawn for one seat before the index is taken to be failing rather than full.</summary>
    private const int CodeDraws = 16;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, PlayedTable> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayedTable> _byToken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayedTable> _byInvitation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<PlayedTable?>> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _legacy = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _refusedUntil = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private DateTimeOffset _swept = DateTimeOffset.MinValue;
    private int _reserved;

    /// <param name="index">The index of the tables this host lists, counts and finds by.</param>
    /// <param name="mostUnderWay">How many tables may be under way at once; a test lowers it to reach the bound.</param>
    public TableRegistry(ITableIndex index, int mostUnderWay = MostUnderWay)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(mostUnderWay, 1);
        Index = index;
        Capacity = mostUnderWay;
    }

    public ITableIndex Index { get; }

    /// <summary>How many tables may be under way at once.</summary>
    public int Capacity { get; }

    /// <summary>What picks up a recorded table this host does not hold; none on a host that keeps nothing, which holds every table it has.</summary>
    public TableLoader? Loader { get; init; }

    /// <summary>
    /// The basis this host replays tables on (ADR 0091): a table refused under it is the operator's, and is neither
    /// listed nor counted here; one refused under another basis is tried again (ADR 0115).
    /// </summary>
    public ReplayBasis? Basis { get; init; }

    /// <summary>Whether a failure is the index's or the store's being out, which a request reads past rather than fails on.</summary>
    internal static bool StoreFails(Exception failure) =>
        failure is IOException or UnauthorizedAccessException or Azure.RequestFailedException;

    /// <summary>
    /// The open tables of the index, or none when it does not answer: what is held here is still counted and
    /// listed, and the line on the console says the rest is missing.
    /// </summary>
    private async Task<IReadOnlyList<IndexedTable>> ListedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Index.OpenAsync(0, Listing, cancellationToken);
        }
        catch (Exception failure) when (StoreFails(failure))
        {
            Console.WriteLine($"  The index of the tables did not answer ({failure.Message}): only the tables this host holds are listed.");
            return [];
        }
    }

    /// <summary>Whether an index entry is a table this host leaves alone: closed by it, or refused under its basis.</summary>
    private bool LeftAlone(string id, TableSummary? summary) =>
        _gone.Contains(id) || (summary?.RefusedUnder is { } refused && refused == Basis);

    /// <summary>
    /// A place for one more table, taken before the table is composed, or none when the tables are as many as the
    /// cap. Composing a table opens its recording and starts its match, so the place has to be held first: two
    /// requests arriving for the last one would otherwise both compose, and one would be refused having written a
    /// run nobody played. A reservation not handed to <see cref="AddAsync" /> is given back when it is disposed.
    /// </summary>
    /// <remarks>
    /// The open tables of the index count, and the tables held here that it does not list yet: one added a moment
    /// ago is listed once its record is written. One this host closed does not, whether or not its record has
    /// said so yet. Two hosts reserving the last place at once may both have it.
    /// </remarks>
    public async Task<Reservation?> ReserveAsync(CancellationToken cancellationToken = default)
    {
        var listed = await ListedAsync(cancellationToken);
        lock (_gate)
        {
            var underWay = listed
                .Where(table => !LeftAlone(table.Table, TableSummary.Parse(table.Summary)))
                .Select(table => table.Table)
                .Where(id => !_byId.TryGetValue(id, out var held) || !held.IsOver)
                .Union(_byId.Values.Where(held => !held.IsOver).Select(held => held.Id), StringComparer.Ordinal)
                .Count();
            if (underWay + _reserved >= Capacity)
            {
                return null;
            }

            _reserved++;
            return new Reservation(this);
        }
    }

    /// <summary>
    /// Adds a table into the place reserved for it, giving its seats their codes. A failure of the index is thrown,
    /// with no code left taken and the table not added: the caller lets it go.
    /// </summary>
    public async Task AddAsync(PlayedTable table, Reservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(reservation);
        try
        {
            foreach (var seat in table.Seats)
            {
                seat.Code = await CodeForAsync(table, seat, seat.Code, cancellationToken);
            }
        }
        catch
        {
            // The table is not added: the codes its seats were given go back, as far as the index answers.
            await ForgetCodesAsync(table);
            throw;
        }

        lock (_gate)
        {
            reservation.Spend();
            Register(table);
        }
    }

    /// <summary>
    /// A code taken in the index for a seat a link may still name (<see cref="TableSeat.Key" />), none for a bot's,
    /// a taken one, or one kept for a friend. The one wanted when the index gives it, or holds it for this seat
    /// already, as an undo and a table picked up again want; another one drawn otherwise.
    /// </summary>
    private async Task<string?> CodeForAsync(PlayedTable table, TableSeat seat, string? wanted, CancellationToken cancellationToken)
    {
        if (seat.Key is null)
        {
            return null;
        }

        if (wanted is not null && await TakenAsync(wanted))
        {
            return wanted;
        }

        for (var draw = 0; draw < CodeDraws; draw++)
        {
            if (JoinCodes.Draw() is var code && await TakenAsync(code))
            {
                return code;
            }
        }

        throw new IOException($"No code could be taken in the index for {seat.Name} of session {table.Id} in {CodeDraws} draws.");

        async Task<bool> TakenAsync(string code) =>
            await Index.AddCodeAsync(new IndexedCode(code, table.Id, seat.Name), cancellationToken)
            || await Index.CodeAsync(code, cancellationToken) is { } held && held.Table == table.Id && held.Seat == seat.Name;
    }

    /// <summary>The codes a table's seats hold given back to the index, as far as it answers: a table that is not added.</summary>
    private async Task ForgetCodesAsync(PlayedTable table)
    {
        foreach (var code in table.Seats.Select(seat => seat.Code).OfType<string>())
        {
            try
            {
                await Index.RemoveCodeAsync(code, table.Id, CancellationToken.None);
            }
            catch (Exception failure) when (StoreFails(failure))
            {
                // Left in the index: it names a seat that never had it, and the answer reads the seat.
            }
        }
    }

    /// <summary>Under the lock: the table into every map.</summary>
    private void Register(PlayedTable table)
    {
        // An id names a run's directory and the tokenless session page, so two tables with one id would
        // write into each other and read as each other. Thirty-two random bits a second make this a bug
        // rather than a chance, and a bug is thrown, not routed.
        if (!_byId.TryAdd(table.Id, table))
        {
            throw new InvalidOperationException($"A table with id '{table.Id}' is already at this host.");
        }

        _byToken[table.Pilot.Token] = table;
        table.Api.Record = () => table.RecordAsync();
        foreach (var seat in table.Seats)
        {
            _byToken[seat.Token] = table;
            if (seat.Invitation is { } invitation)
            {
                _byInvitation[invitation] = table;
            }
        }
    }

    /// <summary>A key minted before keys named their table, and the table it reaches: told once, as a host starts.</summary>
    public void Remember(string key, string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        if (TableKeys.TableOf(key) is null)
        {
            lock (_gate)
            {
                _legacy[key] = table;
            }
        }
    }

    /// <summary>A table this host holds, without picking up one it does not.</summary>
    public PlayedTable? Held(string id)
    {
        lock (_gate)
        {
            return _byId.GetValueOrDefault(id);
        }
    }

    /// <summary>Every table this host holds, newest first.</summary>
    public IReadOnlyList<PlayedTable> HeldTables()
    {
        lock (_gate)
        {
            return [.. _byId.Values.OrderByDescending(table => table.CreatedAt)];
        }
    }

    /// <summary>A table by its id: held here, or picked up from its record when it is open and can be.</summary>
    public async Task<PlayedTable?> ByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        Task<PlayedTable?> loading;
        lock (_gate)
        {
            if (_byId.TryGetValue(id, out var held))
            {
                return held;
            }

            if (Loader is null || !TableKeys.IsTableId(id) || _gone.Contains(id) || _stopping.IsCancellationRequested
                || (_refusedUntil.TryGetValue(id, out var until) && Environment.TickCount64 < until))
            {
                return null;
            }

            // One pick-up a table, whoever asks for it meanwhile: a table picked up twice would be two matches
            // over one journal.
            if (!_loading.TryGetValue(id, out var started))
            {
                started = PickUpAsync(id);
                _loading[id] = started;
            }

            loading = started;
        }

        return await loading.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// A table picked up into the cache, on the registry's own time: whoever asked first may stop waiting, the others
    /// may not. One that cannot be picked up is not tried again for <see cref="RefusedFor" />, and a failure is its
    /// own, said on the console: a listing that asked for it shows the others.
    /// </summary>
    private async Task<PlayedTable?> PickUpAsync(string id)
    {
        await Task.Yield();
        PlayedTable? table = null;
        try
        {
            table = await Loader!.LoadAsync(id, _stopping.Token);
            if (table is not null)
            {
                // Its seats' codes, as the record has them, are the index's already: the record wrote them there.
                // A seat with no code yet gets one before anybody can reach it, which the next record write keeps.
                foreach (var seat in table.Seats.Where(seat => seat.Key is not null && seat.Code is null))
                {
                    seat.Code = await CodeForAsync(table, seat, null, _stopping.Token);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The host is stopping: nothing to pick up any more.
        }
        catch (Exception failure) when (TableLoader.Refuses(failure) || StoreFails(failure))
        {
            Console.WriteLine($"  Session {id} was not picked up: {failure.Message}");
        }

        lock (_gate)
        {
            _loading.Remove(id);
            if (table is not null && !_gone.Contains(id) && !_stopping.IsCancellationRequested)
            {
                _refusedUntil.Remove(id);
                Register(table);
                return table;
            }

            if (table is null)
            {
                _refusedUntil[id] = Environment.TickCount64 + (long)RefusedFor.TotalMilliseconds;
            }
        }

        table?.Dispose();
        return null;
    }

    /// <summary>The table a seat or pilot token belongs to, or none.</summary>
    public async Task<PlayedTable?> ByTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string? id;
        lock (_gate)
        {
            if (_byToken.TryGetValue(token, out var held))
            {
                return held;
            }

            id = TableKeys.TableOf(token) ?? _legacy.GetValueOrDefault(token);
        }

        return id is not null && await ByIdAsync(id, cancellationToken) is { } table
            && (table.Pilot.Token == token || table.Seats.Any(seat => seat.Token == token))
            ? table
            : null;
    }

    /// <summary>The table and seat an outstanding invitation names (ADR 0116), or none: a spent one names nothing.</summary>
    public async Task<(PlayedTable Table, TableSeat Seat)?> ByInvitationAsync(string? invitation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invitation))
        {
            return null;
        }

        string? id;
        lock (_gate)
        {
            id = _byInvitation.GetValueOrDefault(invitation)?.Id ?? TableKeys.TableOf(invitation) ?? _legacy.GetValueOrDefault(invitation);
        }

        return id is not null && await ByIdAsync(id, cancellationToken) is { } table
            && table.Seats.FirstOrDefault(seat => seat.Invitation == invitation) is { } seat
            ? (table, seat)
            : null;
    }

    /// <summary>
    /// The answer to a typed code (<see cref="JoinCodes.Answer" />): the seat the index says it opens, when that seat
    /// still has it. The index is a pointer, and the seat is what says: a code a seat was given again, or one whose
    /// seat was taken, reaches nothing however the index stands.
    /// </summary>
    public async Task<StudioResponse> AnswerAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (JoinCodes.In(path) is not { } code || await Index.CodeAsync(code, cancellationToken) is not { } indexed)
        {
            return JoinCodes.Answer(null);
        }

        var table = await ByIdAsync(indexed.Table, cancellationToken);
        return JoinCodes.Answer(table?.Seats.FirstOrDefault(seat => seat.Name == indexed.Seat && seat.Code == code));
    }

    /// <summary>A seat's keys as they are, to put back with <see cref="RekeyAsync" /> when what changed them cannot be saved.</summary>
    public SeatKeys KeysOf(TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        lock (_gate)
        {
            return new SeatKeys(seat.Token, seat.Invitation, seat.Taken, seat.Code, seat.Push, seat.Account, seat.InvitedAccount, seat.InvitedAt);
        }
    }

    /// <summary>
    /// Gives a seat new keys without changing the human agent or its pending question: a fresh token when its
    /// player leaves or another takes it, and an invitation and its code while nobody holds it. The old token,
    /// invitation and code stop reaching the seat at once. A code is taken only for a seat a link may still name;
    /// <see cref="SeatKeys.Code" /> asks for that one back, as an undo does. False, changing nothing, once the
    /// table is no longer here: a sweep or a close that won the race leaves no key to put back.
    /// </summary>
    public async Task<bool> RekeyAsync(PlayedTable table, TableSeat seat, SeatKeys keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(keys);
        if (Held(table.Id) != table)
        {
            return false;
        }

        // The code is the index's before it is the seat's, and taken from the keys the seat is about to have: a
        // seat offered again is named by a link, one taken or kept for a friend by none.
        var offered = new TableSeat(seat.Slot, keys.Token, seat.Person) { Invitation = keys.Invitation, Taken = keys.Taken, InvitedAccount = keys.InvitedAccount };
        var code = await CodeForAsync(table, offered, keys.Code, cancellationToken);
        lock (_gate)
        {
            return _byId.GetValueOrDefault(table.Id) == table && Swap(table, seat, keys, code);
        }
    }

    /// <summary>Under the lock: a held table's seat given these keys and this code, the maps with them.</summary>
    private bool Swap(PlayedTable table, TableSeat seat, SeatKeys keys, string? code)
    {
        _byToken.Remove(seat.Token);
        if (seat.Invitation is { } spent)
        {
            _byInvitation.Remove(spent);
        }

        seat.Given(keys with { Code = code });
        _byToken[keys.Token] = table;
        if (keys.Invitation is { } invitation)
        {
            _byInvitation[invitation] = table;
        }

        return true;
    }

    /// <summary>
    /// Puts back the keys a seat had, as <see cref="KeysOf" /> read them, when what changed them could not be saved:
    /// without asking the index anything, since the code they hold is still the seat's there -- only the record
    /// write that failed would have taken it back. False once the table is no longer here.
    /// </summary>
    public bool Restore(PlayedTable table, TableSeat seat, SeatKeys keys)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(keys);
        lock (_gate)
        {
            return _byId.GetValueOrDefault(table.Id) == table && Swap(table, seat, keys, keys.Code);
        }
    }

    /// <summary>
    /// Every open table, newest first: the index's, each summarized as its record last said, and those held here
    /// as they stand now -- one the index does not list yet among them, and one that is over left out.
    /// </summary>
    public async Task<IReadOnlyList<ListedTable>> OpenAsync(CancellationToken cancellationToken = default)
    {
        var listed = await ListedAsync(cancellationToken);
        var held = HeldTables();
        var byId = held.ToDictionary(table => table.Id, StringComparer.Ordinal);

        // A table this host closed is not listed while its record is on its way, nor if it never lands: the
        // index is a write behind it. Nor is one refused under this host's basis, which is the operator's.
        List<ListedTable> fromIndex;
        lock (_gate)
        {
            fromIndex = [.. listed
                .Where(entry => !byId.ContainsKey(entry.Table))
                .Select(entry => TableSummary.Parse(entry.Summary) is { } summary && !LeftAlone(entry.Table, summary) ? new ListedTable(entry.Table, entry.CreatedAt, summary, Held: null) : null)
                .OfType<ListedTable>()];
        }

        var fromHere = held
            .Where(table => !table.IsOver)
            .Select(table => TableSummary.Of(table) is { } summary ? new ListedTable(table.Id, table.CreatedAt, summary, table) : null)
            .OfType<ListedTable>();
        return [.. fromIndex.Concat(fromHere).OrderByDescending(table => table.CreatedAt).ThenByDescending(table => table.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Closes one table now, held here or picked up to be: its match is stopped if it is still being played, its
    /// record says closed, and its keys stop answering. False when no open table has that id.
    /// </summary>
    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (await ByIdAsync(id, cancellationToken) is null)
        {
            return false;
        }

        PlayedTable? table;
        lock (_gate)
        {
            if (!_byId.Remove(id, out table))
            {
                return false;
            }

            Forget(table);
        }

        table.Close();
        return true;
    }

    /// <summary>
    /// A table whose run was deleted, out of the index for good and never picked up again by this host: no record
    /// says it is open any more, and nothing could say it is closed either.
    /// </summary>
    public async Task DeletedAsync(string id, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _gone.Add(id);
        }

        await Index.CloseAsync(id, createdAt, long.MaxValue, cancellationToken);
    }

    /// <summary>
    /// <see cref="SweepAsync" />, at most once every <see cref="SweepEvery" />: what every request may afford, so a
    /// host whose admin panel nobody opens still lets go of the tables nobody is at.
    /// </summary>
    public Task<IReadOnlyList<string>> SweepIfDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (now - _swept < SweepEvery)
            {
                return Task.FromResult<IReadOnlyList<string>>([]);
            }

            _swept = now;
        }

        return SweepAsync(now, cancellationToken);
    }

    /// <summary>
    /// Lets go of the tables nobody is at any more (see the class remarks), and closes, through its record, every
    /// open table of the index nobody holds that never began and was opened longer ago than <see cref="NeverBegunFor" />.
    /// </summary>
    public async Task<IReadOnlyList<string>> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        List<PlayedTable> gone = [];
        lock (_gate)
        {
            foreach (var table in _byId.Values.ToList())
            {
                var idle = now - table.Touched;
                if ((table.IsFinished && idle >= FinishedFor) || (!table.IsOver && !table.Session.HasBegun && idle >= NeverBegunFor))
                {
                    _byId.Remove(table.Id);
                    Forget(table);
                    gone.Add(table);
                }
            }
        }

        foreach (var table in gone)
        {
            table.Close();
        }

        List<string> swept = [.. gone.Select(table => table.Id)];
        if (Loader is { } loader)
        {
            foreach (var listed in await ListedAsync(cancellationToken))
            {
                if (Held(listed.Table) is null && TableSummary.Parse(listed.Summary) is { Begun: false } && now - listed.CreatedAt >= NeverBegunFor
                    && await ExpiredAsync(loader, listed.Table, now, cancellationToken))
                {
                    swept.Add(listed.Table);
                }
            }
        }

        return swept;
    }

    /// <summary>A table nobody holds closed through its record; a failure is a line on the console, and the next sweep tries again.</summary>
    private async Task<bool> ExpiredAsync(TableLoader loader, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // Picked up meanwhile, or being: it is this host's to sweep as a held table, by its last request. Gone
            // while it is read, so no request picks up a table about to be closed.
            if (_loading.ContainsKey(id) || _byId.ContainsKey(id) || !_gone.Add(id))
            {
                return false;
            }
        }

        var expired = false;
        try
        {
            expired = await loader.ExpireAsync(id, now, cancellationToken);
        }
        catch (Exception failure) when (TableLoader.Refuses(failure) || StoreFails(failure))
        {
            Console.WriteLine($"  Session {id} could not be closed: {failure.Message}");
        }

        if (!expired)
        {
            // Its journal says it began, or it could not be read: a game a request may still pick up.
            lock (_gate)
            {
                _gone.Remove(id);
            }
        }

        return expired;
    }

    /// <summary>
    /// Lets every table go, as a host stopping does: without closing any, so that a host after this one picks up
    /// what was being played (ADR 0091).
    /// </summary>
    public void Dispose()
    {
        List<PlayedTable> all;
        lock (_gate)
        {
            if (_stopping.IsCancellationRequested)
            {
                return;
            }

            _stopping.Cancel();
            all = [.. _byId.Values];
            _byId.Clear();
            _byToken.Clear();
            _byInvitation.Clear();
        }

        foreach (var table in all)
        {
            table.Dispose();
        }

        _stopping.Dispose();
    }

    /// <summary>
    /// A place held for a table being composed. Spent when the table is added, given back when disposed
    /// unspent: a composition that failed must not leave the host one table smaller for ever.
    /// </summary>
    public sealed class Reservation : IDisposable
    {
        private TableRegistry? _registry;

        internal Reservation(TableRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>Under the registry's lock: the place is the table's now.</summary>
        internal void Spend()
        {
            if (_registry is { } registry)
            {
                registry._reserved--;
                _registry = null;
            }
        }

        public void Dispose()
        {
            if (_registry is not { } registry)
            {
                return;
            }

            lock (registry._gate)
            {
                Spend();
            }
        }
    }

    /// <summary>Under the lock: a table's keys out of the maps, and the table not to be picked up again by this host.</summary>
    private void Forget(PlayedTable table)
    {
        _gone.Add(table.Id);
        _byToken.Remove(table.Pilot.Token);
        foreach (var seat in table.Seats)
        {
            _byToken.Remove(seat.Token);
            if (seat.Invitation is { } invitation)
            {
                _byInvitation.Remove(invitation);
            }
        }
    }
}
