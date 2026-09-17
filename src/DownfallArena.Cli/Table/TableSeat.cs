using DownfallArena.Domain.Matches;

namespace DownfallArena.Cli.Table;

/// <summary>
/// One seat as the host knows it: which player it is, the token that proves a request is its own, and the
/// person waiting in it — or none, when a bot is playing it.
/// </summary>
/// <remarks>
/// The token is what makes the boundary a boundary. Both players share one screen in hotseat, so the browser
/// holds both tokens and nothing stops it asking for the other seat; what the token buys is that the *server*
/// never answers two seats in one payload, and that two devices later is a change of who holds which token
/// rather than a change of what the host serves (<c>docs/tabletop/playtest-app.md</c> §4).
/// </remarks>
internal sealed record TableSeat(PlayerSlot Slot, string Token, HumanSeat? Person)
{
    /// <summary>The name a URL and a page use for a slot.</summary>
    public static string NameOf(PlayerSlot slot) => slot == PlayerSlot.Player1 ? "player1" : "player2";

    /// <summary>The slot a URL segment names, or <c>null</c> when it names neither.</summary>
    public static PlayerSlot? SlotOf(string name) => name switch
    {
        "player1" => PlayerSlot.Player1,
        "player2" => PlayerSlot.Player2,
        _ => null,
    };

    public string Name => NameOf(Slot);
}
