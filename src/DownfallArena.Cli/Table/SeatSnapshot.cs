using DownfallArena.Application.Matches.Projections;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What one seat reads, as one moment: the question its person is on, the board and the options that go with it
/// -- both carrying the picks, speeds and intents the seat has answered and the match does not have yet (ADR
/// 0111, ADR 0117) -- and how many intents the other side still holds face down, as a number and nothing else.
/// </summary>
internal sealed record SeatSnapshot(Asking? Waiting, PlayerBoardState Board, PlayerOptions Options, int OpponentIntents)
{
    /// <summary>
    /// How many of a board's intents are still face down. A round keeps every intent it was given
    /// (<c>Round.IntentsOf</c>) and tracks what has been turned over separately, in the actions the activation
    /// cursor has reached, so the count of the one is not the count of the other from the first reveal
    /// onwards. Subtracting here is what keeps the number and the reveal strip from contradicting each other
    /// on the same screen: three face down beside two of them face up is a table nobody would believe.
    /// </summary>
    public static int StillFaceDown(PlayerBoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var revealed = board.RevealedActions.Select(action => action.Actor).ToHashSet();
        return board.Intents.Count(intent => !revealed.Contains(intent.Actor));
    }
}
