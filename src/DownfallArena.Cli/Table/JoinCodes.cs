using System.Security.Cryptography;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One short code per seat a person holds, across every table this host is playing, and the route that turns
/// a typed code into that seat's token.
/// </summary>
/// <remarks>
/// A seat token is 128 random bits because it is the whole fence around a seat (<see cref="TableSeat" />), and
/// nobody types 32 hexadecimal characters into a phone across a table. So the token travels in a link and in
/// the page's storage, where its length costs nothing, and what a player reads off the host's screen is eight
/// characters: <c>&lt;host&gt;/j/K7F2Q9XM</c>. Eight characters of a 32-symbol alphabet is 40 bits, which is
/// not guessable over a network — a thousand tries a second would take about thirty-five years — so the code
/// lives as long as the session rather than being burned on first use, because a code that dies when a phone
/// reloads is a playtest interrupted to read the console again.
///
/// The alphabet leaves out I, L, O and U: the first three are the mistakes people actually make reading a code
/// aloud, and the fourth is left out for the reason Crockford leaves it out.
///
/// The codes are the host's rather than one table's (ADR 0081): a code typed into the host has to reach one
/// seat of one table, so it is minted against everything already minted and forgotten when its table goes.
/// </remarks>
internal sealed class JoinCodes
{
    public const string Prefix = "/j/";

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int Length = 8;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, TableSeat> _seats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal);

    public JoinCodes()
    {
    }

    /// <summary>Mints a code for every seat a person holds. A bot's seat has nobody to hand one to.</summary>
    public JoinCodes(IEnumerable<TableSeat> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);
        foreach (var seat in seats)
        {
            Mint(seat);
        }
    }

    /// <summary>
    /// Mints a code for a seat a person holds, or nothing for a bot's: a bot's seat has nobody to hand one to.
    /// A code already in use is minted again rather than shared, which at 40 bits is a loop that runs once.
    /// </summary>
    public string? Mint(TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        if (seat.Person is null)
        {
            return null;
        }

        lock (_gate)
        {
            string code;
            do
            {
                code = RandomNumberGenerator.GetString(Alphabet, Length);
            }
            while (_seats.ContainsKey(code));

            _seats[code] = seat;
            _codes[seat.Token] = code;
            return code;
        }
    }

    /// <summary>
    /// Mints the code a seat had before this host existed (ADR 0091), so what its player wrote down still
    /// works; a fresh one when that code is already somebody's, which a rebuilt table and a new one can do.
    /// </summary>
    public string? Mint(TableSeat seat, string? wanted)
    {
        ArgumentNullException.ThrowIfNull(seat);
        if (seat.Person is null)
        {
            return null;
        }

        lock (_gate)
        {
            if (wanted is { Length: Length } code && !_seats.ContainsKey(code))
            {
                _seats[code] = seat;
                _codes[seat.Token] = code;
                return code;
            }
        }

        return Mint(seat);
    }

    /// <summary>Forgets a seat's code, once its table is gone: a code that still answered would name a seat nobody can play.</summary>
    public void Forget(TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        lock (_gate)
        {
            if (_codes.Remove(seat.Token, out var code))
            {
                _seats.Remove(code);
            }
        }
    }

    /// <summary>The code this seat's player types, or <c>null</c> when a bot holds the seat.</summary>
    public string? Of(TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        lock (_gate)
        {
            return _codes.GetValueOrDefault(seat.Token);
        }
    }

    public static bool Names(string path) => path.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The answer to a typed code: the page, with that seat's token on it. It is a redirect rather than the page
    /// itself so the browser lands on the link the host would have printed — the page then keeps the token and
    /// tidies the address bar, and a reload works without the code.
    /// </summary>
    public StudioResponse Answer(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var typed = Typed(path[Prefix.Length..].TrimEnd('/'));

        TableSeat? seat;
        lock (_gate)
        {
            _seats.TryGetValue(typed, out seat);
        }

        // A code that names no seat is a typo, and a typo is not told which half it got right.
        return seat is not null
            ? StudioResponse.OfRedirect($"/?{seat.Name}={seat.Token}")
            : StudioResponse.OfPlainText(404, "That code does not name a seat at this table. Read it again from the host's screen.");
    }

    /// <summary>
    /// A code as the person typed it, read back as the code that was minted: case is not information, and the
    /// four characters the alphabet leaves out are the ones a reader substitutes for the ones it keeps.
    /// </summary>
    private static string Typed(string code) =>
        string.Concat(code.ToUpperInvariant().Select(character => character switch
        {
            'I' or 'L' => '1',
            'O' => '0',
            'U' => 'V',
            _ => character,
        }));
}
