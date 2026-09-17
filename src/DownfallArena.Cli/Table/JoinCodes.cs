using System.Security.Cryptography;
using DownfallArena.Cli.Studio;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One short code per seat a person holds, and the route that turns a typed code into that seat's token.
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
/// </remarks>
internal sealed class JoinCodes
{
    public const string Prefix = "/j/";

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int Length = 8;

    private readonly Dictionary<string, TableSeat> _seats = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerSlot, string> _codes = [];

    /// <summary>Mints a code for every seat a person holds. A bot's seat has nobody to hand one to.</summary>
    public JoinCodes(IEnumerable<TableSeat> seats)
    {
        ArgumentNullException.ThrowIfNull(seats);
        foreach (var seat in seats.Where(seat => seat.Person is not null))
        {
            var code = RandomNumberGenerator.GetString(Alphabet, Length);
            _seats[code] = seat;
            _codes[seat.Slot] = code;
        }
    }

    /// <summary>The code this seat's player types, or <c>null</c> when a bot holds the seat.</summary>
    public string? Of(TableSeat seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        return _codes.GetValueOrDefault(seat.Slot);
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

        // A code that names no seat is a typo, and a typo is not told which half it got right.
        return _seats.TryGetValue(typed, out var seat)
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
