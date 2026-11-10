using DownfallArena.Application.Matches.Decisions;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Matches.Driving;

/// <summary>
/// The picks, speeds and intents each seat has answered that the match does not have yet. Both seats are asked
/// theirs at once and the answers reach the match together, seat 1's first, once both are done (ADR 0092,
/// ADR 0111, ADR 0117). Between two steps this is the only thing about a match that is not in the match.
/// </summary>
public sealed record HeldAnswers
{
    private HeldAnswers(IReadOnlyList<PlayerDecision> player1, IReadOnlyList<PlayerDecision> player2)
    {
        Player1 = player1;
        Player2 = player2;
    }

    public static HeldAnswers None { get; } = new([], []);

    public IReadOnlyList<PlayerDecision> Player1 { get; }

    public IReadOnlyList<PlayerDecision> Player2 { get; }

    public bool IsEmpty => Player1.Count == 0 && Player2.Count == 0;

    public IReadOnlyList<PlayerDecision> Of(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1 : Player2;

    /// <summary>These answers and one more, after the seat's others.</summary>
    public HeldAnswers With(PlayerSlot slot, PlayerDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return slot == PlayerSlot.Player1
            ? new HeldAnswers([.. Player1, decision], Player2)
            : new HeldAnswers(Player1, [.. Player2, decision]);
    }

    public bool Equals(HeldAnswers? other) =>
        other is not null && Player1.SequenceEqual(other.Player1) && Player2.SequenceEqual(other.Player2);

    public override int GetHashCode() => HashCode.Combine(Player1.Count, Player2.Count);
}
