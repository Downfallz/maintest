namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// Whether the Evolution sub-phase is complete, and how many picks each player can still make.
/// </summary>
public sealed record EvolutionGateResult(bool CanAdvance, int Player1RemainingPicks, int Player2RemainingPicks)
{
    public int RemainingPicksOf(PlayerSlot slot) => slot == PlayerSlot.Player1 ? Player1RemainingPicks : Player2RemainingPicks;
}
