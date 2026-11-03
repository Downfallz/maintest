namespace DownfallArena.Application.Leaderboard;

/// <summary>
/// Mark Glickman's Glicko-2 rating system ("Example of the Glicko-2 system", 2012), step by step, for one player
/// and the games of one rating period (ADR 0124). Pure arithmetic: no clock, no store.
/// </summary>
public static class Glicko2
{
    /// <summary>The deviation of a player nobody knows anything about, and the most any player's can grow to.</summary>
    public const double MaximumDeviation = 350;

    /// <summary>Constrains how fast volatility moves; Glickman suggests 0.3 to 1.2, smaller for games that upset less.</summary>
    public const double Tau = 0.5;

    private const double Scale = 173.7178;
    private const double Convergence = 0.000001;

    /// <summary>
    /// The player's rating after the games of one period, each against an opponent as rated before the period,
    /// scored 1 for a win, 0.5 for a draw and 0 for a loss. With no games, only the deviation grows (step 6).
    /// </summary>
    public static GlickoRating Rate(GlickoRating player, IReadOnlyList<(GlickoRating Opponent, double Score)> games, double tau = Tau)
    {
        ArgumentNullException.ThrowIfNull(games);
        if (games.Count == 0)
        {
            return Idle(player, 1);
        }

        var mu = (player.Rating - 1500) / Scale;
        var phi = player.Deviation / Scale;
        var sigma = player.Volatility;

        // Steps 3 and 4: the estimated variance from the games alone, and the improvement they suggest.
        var inverseVariance = 0.0;
        var sum = 0.0;
        foreach (var (opponent, score) in games)
        {
            var muJ = (opponent.Rating - 1500) / Scale;
            var g = G(opponent.Deviation / Scale);
            var expected = 1 / (1 + Math.Exp(-g * (mu - muJ)));
            inverseVariance += g * g * expected * (1 - expected);
            sum += g * (score - expected);
        }

        var v = 1 / inverseVariance;
        var delta = v * sum;

        // Step 5: the new volatility, by the Illinois algorithm.
        var newSigma = Volatility(delta, phi, v, sigma, tau);

        // Steps 6 to 8: the deviation grows by the volatility, then shrinks by what the games told.
        var phiStar = Math.Sqrt((phi * phi) + (newSigma * newSigma));
        var newPhi = 1 / Math.Sqrt((1 / (phiStar * phiStar)) + (1 / v));
        var newMu = mu + (newPhi * newPhi * sum);
        return new GlickoRating((newMu * Scale) + 1500, Math.Min(newPhi * Scale, MaximumDeviation), newSigma);
    }

    /// <summary>The rating after <paramref name="periods" /> periods without a game: the same rating, less certain.</summary>
    public static GlickoRating Idle(GlickoRating player, int periods)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(periods);
        var phi = player.Deviation / Scale;
        var grown = Math.Sqrt((phi * phi) + (periods * player.Volatility * player.Volatility)) * Scale;
        return player with { Deviation = Math.Min(grown, MaximumDeviation) };
    }

    private static double G(double phi) => 1 / Math.Sqrt(1 + (3 * phi * phi / (Math.PI * Math.PI)));

    private static double Volatility(double delta, double phi, double v, double sigma, double tau)
    {
        var a = Math.Log(sigma * sigma);
        double F(double x)
        {
            var ex = Math.Exp(x);
            var d = (phi * phi) + v + ex;
            return (ex * ((delta * delta) - (phi * phi) - v - ex) / (2 * d * d)) - ((x - a) / (tau * tau));
        }

        var upper = a;
        double lower;
        if (delta * delta > (phi * phi) + v)
        {
            lower = Math.Log((delta * delta) - (phi * phi) - v);
        }
        else
        {
            var k = 1;
            while (F(a - (k * tau)) < 0)
            {
                k++;
            }

            lower = a - (k * tau);
        }

        var fUpper = F(upper);
        var fLower = F(lower);
        while (Math.Abs(lower - upper) > Convergence)
        {
            var c = upper + ((upper - lower) * fUpper / (fLower - fUpper));
            var fC = F(c);
            if (fC * fLower <= 0)
            {
                upper = lower;
                fUpper = fLower;
            }
            else
            {
                fUpper /= 2;
            }

            lower = c;
            fLower = fC;
        }

        return Math.Exp(upper / 2);
    }
}
