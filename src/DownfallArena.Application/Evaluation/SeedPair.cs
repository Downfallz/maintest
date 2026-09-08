using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;

namespace DownfallArena.Application.Evaluation;

/// <summary>
/// One benchmark seed played twice: agent A as player 1 first, then agent B as player 1. Scores are from A's
/// point of view: a win is 1, a draw one half, a loss 0.
/// </summary>
public sealed record SeedPair(int Seed, MatchResult AFirst, MatchResult BFirst)
{
    public double ScoreOfA => (Score(AFirst, PlayerSlot.Player1) + Score(BFirst, PlayerSlot.Player2)) / 2;

    public int WinsOfA => Wins(AFirst, PlayerSlot.Player1) + Wins(BFirst, PlayerSlot.Player2);

    public int WinsOfB => Wins(AFirst, PlayerSlot.Player2) + Wins(BFirst, PlayerSlot.Player1);

    public int Draws => (AFirst.Outcome.IsDraw ? 1 : 0) + (BFirst.Outcome.IsDraw ? 1 : 0);

    /// <summary>The health A kept, summed over its two matches.</summary>
    public int RemainingHealthOfA => AFirst.Player1RemainingHealth + BFirst.Player2RemainingHealth;

    public int RemainingHealthOfB => AFirst.Player2RemainingHealth + BFirst.Player1RemainingHealth;

    private static int Wins(MatchResult result, PlayerSlot slot) => result.Outcome.Winner == slot ? 1 : 0;

    private static double Score(MatchResult result, PlayerSlot slot)
    {
        if (result.Outcome.IsDraw)
        {
            return 0.5;
        }

        return result.Outcome.Winner == slot ? 1 : 0;
    }
}
