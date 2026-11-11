using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// That something a lobby shows changed, for the pages waiting to hear it (ADR 0163): the public lists of games and
/// open seats, for everybody, and one account's own games, invitations and friends, for that account. Like a
/// table's stream (ADR 0119) it says only to read again, never what changed; the lobby's routes still answer.
/// </summary>
/// <remarks>
/// A table is read for the lobby by its shape: whether it is over, and per seat who holds it, who it is kept for,
/// who declined it, whether its person has arrived and whether they are being asked something. Most of a table's
/// changes are a step of its match, and move none of that: those reach the seat streams and not the lobby, so a
/// lobby in view reads its lists when a game begins, ends, gains a player or waits on one, not at every slot.
/// </remarks>
internal sealed class LobbyChanges
{
    private readonly TableChanges _everyone = new();
    private readonly ConcurrentDictionary<AccountId, TableChanges> _accounts = new();

    // The shape each held table was last read in, kept on the table's own lifetime.
    private readonly ConditionalWeakTable<PlayedTable, StrongBox<Shape?>> _shapes = [];

    /// <summary>How many times the public lists changed.</summary>
    public long Version => _everyone.Version;

    /// <summary>How many times one account's own lists changed; 0 for one nothing was said of yet.</summary>
    public long VersionOf(AccountId account) => _accounts.TryGetValue(account, out var changes) ? changes.Version : 0;

    /// <summary>How often the stream says it is still there, as a table's does (<see cref="TableApi.KeepAlive" />).</summary>
    public TimeSpan KeepAlive { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>The public lists changed: a table was opened, closed or let go of.</summary>
    public void Everyone() => _everyone.Bump();

    /// <summary>One account's own lists changed: a friend's request, an invitation, a result kept.</summary>
    public void Account(AccountId? account)
    {
        if (account is { } id)
        {
            Of(id).Bump();
        }
    }

    /// <summary>
    /// From now on, each change of the table's shape is told to everybody and to each account that had or has a
    /// part in it; and once now, since a table newly held is one the lists may not show yet.
    /// </summary>
    public void Watch(PlayedTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var shape = Shape.Of(table);
        _shapes.AddOrUpdate(table, new StrongBox<Shape?>(shape));
        table.Api.Changes.Bumped += () => Changed(table);
        Told(null, shape);
    }

    private void Changed(PlayedTable table)
    {
        var next = table.Api.Changes.IsClosed ? Shape.Closed : Shape.Of(table);
        var box = _shapes.GetOrCreateValue(table);
        Shape? previous;
        lock (box)
        {
            previous = box.Value;
            if (previous == next)
            {
                return;
            }

            box.Value = next;
        }

        Told(previous, next);
    }

    // The public lists, and every account the table names before or after: one that was invited and no longer is
    // has as much to read again as the one invited now.
    private void Told(Shape? previous, Shape next)
    {
        _everyone.Bump();
        foreach (var account in (previous?.Accounts ?? []).Concat(next.Accounts).Distinct())
        {
            Of(account).Bump();
        }
    }

    private TableChanges Of(AccountId account) => _accounts.GetOrAdd(account, _ => new TableChanges());

    /// <summary>
    /// The stream of <c>GET /api/lobby/events</c>: <c>ready</c>, then <c>changed</c> with <c>lobby</c> when the
    /// public lists moved and <c>account</c> when the signed-in player's own did, and a comment every
    /// <see cref="KeepAlive" /> while neither does. It ends when the browser goes.
    /// </summary>
    public async Task StreamAsync(Stream stream, AccountId? account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var mine = account is { } id ? Of(id) : null;
        var seenLobby = _everyone.Version;
        var seenMine = mine?.Version ?? 0;
        await WriteAsync(stream, "event: ready\ndata: 0\n\n", cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var lobby = _everyone.WaitAsync(seenLobby, cancellationToken);
            var own = mine?.WaitAsync(seenMine, cancellationToken);
            var quiet = Task.Delay(KeepAlive, cancellationToken);
            var first = own is null ? await Task.WhenAny(lobby, quiet) : await Task.WhenAny(lobby, own, quiet);
            if (first == quiet)
            {
                await quiet;
                await WriteAsync(stream, ": keep-alive\n\n", cancellationToken);
                continue;
            }

            if (lobby.IsCompletedSuccessfully && lobby.Result > seenLobby)
            {
                seenLobby = lobby.Result;
                await WriteAsync(stream, $"event: changed\ndata: lobby {seenLobby.ToString(CultureInfo.InvariantCulture)}\n\n", cancellationToken);
            }

            if (own is { IsCompletedSuccessfully: true } && own.Result > seenMine)
            {
                seenMine = own.Result;
                await WriteAsync(stream, $"event: changed\ndata: account {seenMine.ToString(CultureInfo.InvariantCulture)}\n\n", cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static async Task WriteAsync(Stream stream, string text, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(text), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>What of a table a lobby shows, as one comparable line.</summary>
    private sealed record Shape(string Line, IReadOnlyList<AccountId> Accounts)
    {
        public static Shape Closed { get; } = new("closed", []);

        public static Shape Of(PlayedTable table)
        {
            var seats = table.Seats.Select(seat => string.Join(',',
                seat.Name,
                seat.Account?.Value ?? "-",
                seat.InvitedAccount?.Value ?? "-",
                seat.DeclinedBy?.Value ?? "-",
                seat.Taken ? "taken" : "free",
                table.OpenSeats.Contains(seat.Name) ? "open" : "kept",
                seat.Person is { } person ? (person.HasArrived ? "here" : "away") + (person.Waiting is null ? "" : "+asked") : "bot"));
            AccountId[] accounts = [.. table.Seats
                .SelectMany(seat => new[] { seat.Account, seat.InvitedAccount, seat.DeclinedBy })
                .OfType<AccountId>()
                .Distinct()];
            return new Shape($"{(table.IsOver ? "over" : "on")}|{string.Join('|', seats)}", accounts);
        }

        public bool Equals(Shape? other) => other is not null && string.Equals(Line, other.Line, StringComparison.Ordinal);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Line);
    }
}
