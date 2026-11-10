using DownfallArena.Application.Agents;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// Who decides for each seat while a match is stepped: a Player agent, or nobody here, which is a person whose
/// decisions are submitted as they arrive.
/// </summary>
public sealed record MatchSeats(IPlayerAgent? Player1, IPlayerAgent? Player2)
{
    /// <summary>Both seats answered from outside: two people, or a record being submitted again.</summary>
    public static MatchSeats People { get; } = new(null, null);

    public IPlayerAgent? Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1 : Player2;
}
