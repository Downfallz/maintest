namespace DownfallArena.Application.Accounts;

/// <summary>The same outcome, so many games in a row.</summary>
public sealed record StreakOf(GameOutcome Outcome, int Length);
