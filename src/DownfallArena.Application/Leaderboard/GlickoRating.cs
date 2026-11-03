namespace DownfallArena.Application.Leaderboard;

/// <summary>
/// A player's strength as Glicko-2 states it (ADR 0125), on the familiar scale: a rating, how uncertain it is (the
/// rating deviation, roughly one standard deviation), and how erratic the player has been (the volatility).
/// </summary>
public readonly record struct GlickoRating(double Rating, double Deviation, double Volatility)
{
    /// <summary>Where every player starts: the scale's centre, as uncertain as the system allows.</summary>
    public static readonly GlickoRating Unrated = new(1500, Glicko2.MaximumDeviation, 0.06);
}
