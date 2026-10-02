namespace DownfallArena.Cli.Table;

/// <summary>
/// Every table this host is playing, found by what a request carries: a seat or pilot token, a session id, or
/// a typed join code (ADR 0081).
/// </summary>
/// <remarks>
/// <para>
/// A token names its table because it already names its seat: a seat token is 128 random bits and a pilot
/// token the same, so the map from token to table is the map from token to seat with one more hop, and the
/// page, the transport and the pilot need no session id in any route. What a table is asked through its own
/// API is unchanged; this only says which one.
/// </para>
/// <para>
/// Tables are let go of on a sweep rather than on a timer: a table that is finished and untouched for an
/// hour, or abandoned (never over, nobody polling) for twenty hours, is disposed and its codes forgotten. The
/// sweep runs when the admin panel is asked anything, which is the one moment memory is about to be spent, and at
/// most once a minute from any other request, so a host whose admin panel nobody opens still lets go.
/// </para>
/// </remarks>
internal sealed class TableRegistry : IDisposable
{
    /// <summary>How long a finished table stays readable after the last request: the end screen, the session page.</summary>
    public static readonly TimeSpan FinishedFor = TimeSpan.FromHours(1);

    /// <summary>How long a table nobody asks anything of is kept before it is abandoned: an evening, and a night's sleep.</summary>
    public static readonly TimeSpan IdleFor = TimeSpan.FromHours(20);

    /// <summary>How often a request other than the admin panel's is allowed to sweep.</summary>
    public static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many tables may be under way at once. The admin panel is behind the operator's login, so this bounds a
    /// mistake rather than an attack: a seat blocks a thread while a person thinks (ADR 0054), and a host
    /// with hundreds of them is a host that stopped answering. A table waiting for its players (ADR 0092)
    /// counts: it holds no thread yet, but it will the moment they arrive, and refusing them then would
    /// strand two people who have just sat down, while refusing the operator now is one line on their panel.
    /// </summary>
    public const int MostUnderWay = 16;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, PlayedTable> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayedTable> _byToken = new(StringComparer.Ordinal);
    private DateTimeOffset _swept = DateTimeOffset.MinValue;
    private int _reserved;

    /// <param name="mostUnderWay">How many tables may be under way at once; a test lowers it to reach the bound.</param>
    public TableRegistry(int mostUnderWay = MostUnderWay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mostUnderWay, 1);
        Capacity = mostUnderWay;
    }

    /// <summary>How many tables this host lets be under way at once.</summary>
    public int Capacity { get; }

    /// <summary>The join codes, minted for every seat a person holds as its table is added.</summary>
    public JoinCodes Codes { get; } = new();

    /// <summary>
    /// A place for one more table, taken before the table is composed, or none when the host is full.
    /// Composing a table opens its recording and starts its match, so the place has to be held first: two
    /// requests arriving for the last one would otherwise both compose, and one would be refused having
    /// written a run nobody played. A reservation not handed to <see cref="Add" /> is given back when it is
    /// disposed.
    /// </summary>
    public Reservation? Reserve()
    {
        lock (_gate)
        {
            if (UnderWay() + _reserved >= Capacity)
            {
                return null;
            }

            _reserved++;
            return new Reservation(this);
        }
    }

    /// <summary>Adds a table into the place reserved for it, minting the codes of its seats.</summary>
    public void Add(PlayedTable table, Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(reservation);
        lock (_gate)
        {
            reservation.Spend();
            Register(table);
        }
    }

    /// <summary>Adds a table, minting the codes of its seats. False when the host has as many under way as it takes.</summary>
    public bool TryAdd(PlayedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        lock (_gate)
        {
            if (UnderWay() + _reserved >= Capacity)
            {
                return false;
            }

            Register(table);
            return true;
        }
    }

    /// <summary>
    /// Adds a table rebuilt from its record (ADR 0091), with the codes its seats had. False when the host has
    /// as many under way as it takes, in which case the table is the caller's to let go of.
    /// </summary>
    public bool TryRestore(PlayedTable table, IReadOnlyDictionary<string, string?> codes)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(codes);
        lock (_gate)
        {
            if (UnderWay() + _reserved >= Capacity)
            {
                return false;
            }

            Register(table, codes);
            return true;
        }
    }

    /// <summary>Under the lock: the table into every map, and a code for every seat a person holds.</summary>
    private void Register(PlayedTable table, IReadOnlyDictionary<string, string?>? codes = null)
    {
        // An id names a run's directory and the tokenless session page, so two tables with one id would
        // write into each other and read as each other. Thirty-two random bits a second make this a bug
        // rather than a chance, and a bug is thrown, not routed.
        if (!_byId.TryAdd(table.Id, table))
        {
            throw new InvalidOperationException($"A table with id '{table.Id}' is already at this host.");
        }

        _byToken[table.Pilot.Token] = table;
        table.Api.CodeOf = Codes.Of;
        foreach (var seat in table.Seats)
        {
            _byToken[seat.Token] = table;
            if (codes is not null)
            {
                Codes.Mint(seat, codes.GetValueOrDefault(seat.Name));
            }
            else
            {
                Codes.Mint(seat);
            }
        }
    }

    /// <summary>The table a seat or pilot token belongs to, or none.</summary>
    public PlayedTable? ByToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        lock (_gate)
        {
            return _byToken.GetValueOrDefault(token);
        }
    }

    public PlayedTable? ById(string id)
    {
        lock (_gate)
        {
            return _byId.GetValueOrDefault(id);
        }
    }

    /// <summary>Every table, newest first.</summary>
    public IReadOnlyList<PlayedTable> All()
    {
        lock (_gate)
        {
            return [.. _byId.Values.OrderByDescending(table => table.CreatedAt)];
        }
    }

    /// <summary>Drops one table now: its match is stopped if it is still being played, and its codes stop answering.</summary>
    public bool Remove(string id)
    {
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
    /// <see cref="Sweep" />, at most once every <see cref="SweepEvery" />: what every request may afford, so a
    /// host whose admin panel nobody opens still lets go of the tables nobody is at.
    /// </summary>
    public IReadOnlyList<string> SweepIfDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (now - _swept < SweepEvery)
            {
                return [];
            }

            _swept = now;
        }

        return Sweep(now);
    }

    /// <summary>Lets go of the tables nobody is at any more. See the class remarks for which those are.</summary>
    public IReadOnlyList<string> Sweep(DateTimeOffset now)
    {
        List<PlayedTable> gone = [];
        lock (_gate)
        {
            foreach (var table in _byId.Values.ToList())
            {
                var idle = now - table.Touched;
                if ((table.IsFinished && idle >= FinishedFor) || (!table.IsOver && idle >= IdleFor))
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

        return [.. gone.Select(table => table.Id)];
    }

    /// <summary>
    /// Lets every table go, as a host stopping does: without closing any, so that the host after this one
    /// rebuilds what was being played (ADR 0091).
    /// </summary>
    public void Dispose()
    {
        List<PlayedTable> all;
        lock (_gate)
        {
            all = [.. _byId.Values];
            _byId.Clear();
            _byToken.Clear();
        }

        foreach (var table in all)
        {
            table.Dispose();
        }
    }

    private int UnderWay() => _byId.Values.Count(held => !held.IsOver);

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

    private void Forget(PlayedTable table)
    {
        _byToken.Remove(table.Pilot.Token);
        foreach (var seat in table.Seats)
        {
            _byToken.Remove(seat.Token);
            Codes.Forget(seat);
        }
    }
}
