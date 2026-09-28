namespace DownfallArena.Domain.Matches.Rules;

/// <summary>
/// The win condition of ADR 0011: a defeated team loses (both defeated is a draw), checked whenever health
/// changes (ADR 0083); otherwise, at the end of the round cap, the highest total remaining health wins and
/// equality is a draw.
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

        if (Elimination(player1, player2) is { } eliminated)
        {
            return eliminated;
        }

        if (completedRound >= rules.RoundCap)
        {
            return new MatchOutcome(Healthier(player1, player2), MatchEndReason.RoundCap);
        }

        return null;
    }

    /// <summary>
    /// The outcome when a team has been wiped, or <c>null</c> while both still stand. The match reads it after
    /// every action and every upkeep, and ends at once on it (ADR 0083).
    /// </summary>
    public static MatchOutcome? Elimination(Team player1, Team player2)
    {
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);

        return Elimination(player1.IsDefeated, player2.IsDefeated);
    }

    /// <summary>
    /// The same outcome from whether each side is defeated, for a reader that knows it without forming the
    /// teams: a hypothetical board read after every slot of a rollout (ADR 0083).
    /// </summary>
    public static MatchOutcome? Elimination(bool player1Defeated, bool player2Defeated)
    {
        if (!player1Defeated && !player2Defeated)
        {
            return null;
        }

        PlayerSlot? survivor = player1Defeated && player2Defeated ? null : player1Defeated ? PlayerSlot.Player2 : PlayerSlot.Player1;
        return new MatchOutcome(survivor, MatchEndReason.Elimination);
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
