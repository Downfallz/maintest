namespace DownfallArena.Application.Leaderboard;

/// <summary>What a player's games against one bot came to: never rated, since a bot is not a person on the ladder.</summary>
public sealed record BotRecord(string Agent, int Wins, int Losses, int Draws)
{
    public int Games => Wins + Losses + Draws;
}
