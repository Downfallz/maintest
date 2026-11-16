using System.Security.Cryptography;
using DownfallArena.Cli.Studio;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The short code a seat a person has yet to take is reached by, and how a typed one is read back: the codes
/// themselves are the seats' keys (<see cref="TableSeat.Code" />), and the index of the tables finds the seat
/// a code names (ADR 0161).
/// </summary>
/// <remarks>
/// A seat token is 128 random bits because it is the whole fence around a seat (<see cref="TableSeat" />), and
/// nobody types 32 hexadecimal characters into a phone across a table. So the token travels in a link and in
/// the page's storage, where its length costs nothing, and what a player reads off the host's screen is eight
/// characters: <c>&lt;host&gt;/j/K7F2Q9XM</c>. Eight characters of a 32-symbol alphabet is 40 bits, which is
/// not guessable over a network — a thousand tries a second would take about thirty-five years. A code names the
/// seat's invitation, not its token, and goes with it (ADR 0116): the first browser to confirm takes the seat and
/// keeps its token, so a reload needs no code, and a second browser typing the same code finds it spent rather
/// than a second way into a seat somebody is playing.
///
/// The alphabet leaves out I, L, O and U: the first three are the mistakes people actually make reading a code
/// aloud, and the fourth is left out for the reason Crockford leaves it out.
///
/// The codes are every host's rather than one table's (ADR 0081, ADR 0161): a code typed into any host has to
/// reach one seat of one table, so a code is taken in the index before a seat is given it, and another is drawn
/// when it is somebody's already.
/// </remarks>
internal static class JoinCodes
{
    public const string Prefix = "/j/";

    /// <summary>
    /// Where a code that names no open seat sends the browser: the table page, which says the invitation can no
    /// longer be used and offers the game this browser kept, which is what a player tapping their own link again
    /// is after (ADR 0116). A redirect, so a link in a chat lands on the page rather than on a bare refusal.
    /// </summary>
    public const string Spent = "/?invitation=spent";

    /// <summary>How many characters a code has.</summary>
    public const int Length = 8;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>A code nobody may have yet: 40 random bits, which the index then says are nobody's or are taken.</summary>
    public static string Draw() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static bool Names(string path) => path.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The code a <c>/j/&lt;code&gt;</c> path carries, read back as the code that was minted; none when it cannot be one.</summary>
    public static string? In(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Names(path))
        {
            return null;
        }

        var typed = Typed(path[Prefix.Length..].TrimEnd('/'));
        return typed.Length == Length && typed.All(Alphabet.Contains) ? typed : null;
    }

    /// <summary>
    /// The answer to a typed code: the page, with that seat's key on it, or <see cref="Spent" /> when the code names
    /// no seat a link may still name. It is a redirect rather than the page itself so the browser lands on the link
    /// the host would have printed -- the page then keeps the key and tidies the address bar, and a reload works
    /// without the code. A code that names no seat is a typo, or one already spent (ADR 0116), and neither is told which.
    /// </summary>
    public static StudioResponse Answer(TableSeat? seat) =>
        seat?.Key is { } key
            ? StudioResponse.OfRedirect($"/?{seat.Name}={key}")
            : StudioResponse.OfRedirect(Spent);

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
