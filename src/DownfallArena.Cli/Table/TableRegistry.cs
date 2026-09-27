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
/// hour, or abandoned (never over, nobody polling) for a day, is disposed and its codes forgotten. The sweep
/// runs when the lobby is asked anything, which is the one moment memory is about to be spent.
/// </para>
/// </remarks>
internal sealed class TableRegistry : IDisposable
{
    /// <summary>How long a finished table stays readable after the last request: the end screen, the session page.</summary>
    public static readonly TimeSpan FinishedFor = TimeSpan.FromHours(1);

    /// <summary>How long a table nobody asks anything of is kept before it is abandoned: an evening, and a night's sleep.</summary>
    public static readonly TimeSpan IdleFor = TimeSpan.FromHours(20);

    /// <summary>
    /// How many tables may be under way at once. The lobby is behind the operator's login, so this bounds a
    /// mistake rather than an attack: a seat blocks a thread while a person thinks (ADR 0054), and a host
    /// with hundreds of them is a host that stopped answering.
    /// </summary>
    public const int MostUnderWay = 16;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, PlayedTable> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlayedTable> _byToken = new(StringComparer.Ordinal);
    private readonly JoinCodes _codes = new();
    private readonly int _mostUnderWay;

    /// <param name="mostUnderWay">How many tables may be under way at once; a test lowers it to reach the bound.</param>
    public TableRegistry(int mostUnderWay = MostUnderWay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mostUnderWay, 1);
        _mostUnderWay = mostUnderWay;
    }

    /// <summary>How many tables this host lets be under way at once.</summary>
    public int Capacity => _mostUnderWay;

    /// <summary>The join codes, minted for every seat a person holds as its table is added.</summary>
    public JoinCodes Codes => _codes;

    /// <summary>Adds a table, minting the codes of its seats. False when the host has as many under way as it takes.</summary>
    public bool TryAdd(PlayedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        lock (_gate)
        {
            if (_byId.Values.Count(held => !held.IsOver) >= _mostUnderWay)
            {
                return false;
            }

            _byId[table.Id] = table;
            _byToken[table.Pilot.Token] = table;
            foreach (var seat in table.Seats)
            {
                _byToken[seat.Token] = table;
                _codes.Mint(seat);
            }

            return true;
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

        table.Dispose();
        return true;
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
            table.Dispose();
        }

        return [.. gone.Select(table => table.Id)];
    }

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

    private void Forget(PlayedTable table)
    {
        _byToken.Remove(table.Pilot.Token);
        foreach (var seat in table.Seats)
        {
            _byToken.Remove(seat.Token);
            _codes.Forget(seat);
        }
    }
}
