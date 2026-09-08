namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// The win condition of ADR 0011, checked at the end of every round: a defeated team loses (both defeated is a
/// draw); otherwise, at the round cap, the highest total remaining health wins and equality is a draw.
/// </summary>
public static class WinCondition
{
    /// <summary>
    /// The outcome if the match ends after this round, or <c>null</c> when it continues.
    /// </summary>
    public static MatchOutcome? Evaluate(Team player1, Team player2, int completedRound, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);
        ArgumentNullException.ThrowIfNull(rules);

        if (player1.IsDefeated || player2.IsDefeated)
        {
            return new MatchOutcome(Survivor(player1, player2), MatchEndReason.Elimination);
        }

        if (completedRound >= rules.RoundCap)
        {
            return new MatchOutcome(Healthier(player1, player2), MatchEndReason.RoundCap);
        }

        return null;
    }

    private static PlayerSlot? Survivor(Team player1, Team player2)
    {
        if (player1.IsDefeated && player2.IsDefeated)
        {
            return null;
        }

        return player1.IsDefeated ? player2.Owner : player1.Owner;
    }

    private static PlayerSlot? Healthier(Team player1, Team player2)
    {
        if (player1.TotalHealth == player2.TotalHealth)
        {
            return null;
        }

        return player1.TotalHealth > player2.TotalHealth ? player1.Owner : player2.Owner;
    }
}
