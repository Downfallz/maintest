using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Leaderboard;

/// <summary>One person on the ladder (ADR 0125): their rating now, and what their ranked games came to.</summary>
public sealed record Standing(AccountId Account, GlickoRating Rating, int Wins, int Losses, int Draws, DateTimeOffset LastPlayed)
{
    /// <summary>A deviation above this is a rating still being found: the player is listed as provisional.</summary>
    public const double ProvisionalDeviation = 110;

    public int Games => Wins + Losses + Draws;

    public bool Provisional => Rating.Deviation > ProvisionalDeviation;
}
