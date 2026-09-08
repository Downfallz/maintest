namespace DownfallArena.Application.Evaluation;

/// <summary>
/// What an evaluation says about one agent: its win rate with the paired interval, the health it keeps, which
/// spells it declares and how evenly (entropy in bits), and how its actions resolve.
/// </summary>
public sealed record AgentReport
{
    public required string Agent { get; init; }

    public required int Wins { get; init; }

    public required ConfidenceInterval WinRate { get; init; }

    /// <summary>The mean per-pair score (win 1, draw one half, loss 0) with its interval; one half means even.</summary>
    public required ConfidenceInterval Score { get; init; }

    public required double AverageRemainingHealth { get; init; }

    /// <summary>Intents declared per spell id.</summary>
    public required IReadOnlyDictionary<string, int> SpellUsage { get; init; }

    public required double SpellEntropy { get; init; }

    public required int Actions { get; init; }

    public required int Fizzles { get; init; }

    public required int Criticals { get; init; }

    public double FizzleRate => Actions == 0 ? 0 : (double)Fizzles / Actions;

    public double CriticalRate => Actions == 0 ? 0 : (double)Criticals / Actions;
}
