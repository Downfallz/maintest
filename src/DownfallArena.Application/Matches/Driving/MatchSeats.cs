using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Who decides for each seat while a match is stepped: a Player agent, or nobody here, which is a person whose
/// decisions are submitted as they arrive. A seat can change hands from the top of a round: a step that plays
/// several rounds reads who sits at each decision, off the round the seat's board says.
/// </summary>
public sealed class MatchSeats
{
    private readonly IReadOnlyList<Sitting> _player1;
    private readonly IReadOnlyList<Sitting> _player2;

    /// <summary>The same occupants for the whole match.</summary>
    public MatchSeats(IPlayerAgent? player1, IPlayerAgent? player2)
        : this([new Sitting(0, player1)], [new Sitting(0, player2)])
    {
    }

    private MatchSeats(IReadOnlyList<Sitting> player1, IReadOnlyList<Sitting> player2)
    {
        _player1 = player1;
        _player2 = player2;
    }

    /// <summary>Both seats answered from outside: two people, or a record being submitted again.</summary>
    public static MatchSeats People { get; } = new((IPlayerAgent?)null, null);

    /// <summary>Who decides for a seat at a round: whoever took it last at or before the top of that round.</summary>
    /// <param name="round">The round the seat's board says; none before the first round, which is the seat's first occupant.</param>
    public IPlayerAgent? Of(PlayerSlot slot, int? round = null)
    {
        var sittings = slot == PlayerSlot.Player1 ? _player1 : _player2;
        return sittings.Last(sitting => sitting.FromRound <= (round ?? 0)).Agent;
    }

    /// <summary>
    /// These seats with one changing hands from the top of a round on. Whatever was to sit there from that round
    /// or a later one is replaced: the last thing said about a seat is what holds from then.
    /// </summary>
    public MatchSeats From(PlayerSlot slot, int round, IPlayerAgent? agent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        return slot == PlayerSlot.Player1
            ? new MatchSeats(Changed(_player1, round, agent), _player2)
            : new MatchSeats(_player1, Changed(_player2, round, agent));
    }

    private static List<Sitting> Changed(IReadOnlyList<Sitting> sittings, int round, IPlayerAgent? agent) =>
        [.. sittings.Where(sitting => sitting.FromRound < round), new Sitting(round, agent)];

    private sealed record Sitting(int FromRound, IPlayerAgent? Agent);
}
