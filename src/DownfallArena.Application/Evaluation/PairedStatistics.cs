namespace DownfallArena.Application.Evaluation;

/// <summary>
/// The statistics of an evaluation. The two mirrored matches of a seed are paired, not independent, so an
/// interval is computed over the per-pair means: perfectly correlated pairs then widen it instead of being
/// counted twice.
/// </summary>
public static class PairedStatistics
{
    /// <summary>The 97.5th percentile of the normal distribution: a 95% two-sided interval.</summary>
    public const double Z95 = 1.959964;

    /// <summary>
    /// The mean of the values and its normal 95% interval from the sample standard deviation, clipped to
    /// [0, 1] when the values are rates. One value gives a degenerate interval at the mean; none gives zero.
    /// </summary>
    public static ConfidenceInterval Interval(IReadOnlyList<double> values, bool clipToUnit = true)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return new ConfidenceInterval(0, 0, 0);
        }

        var mean = values.Average();
        var variance = values.Count < 2 ? 0 : values.Sum(value => (value - mean) * (value - mean)) / (values.Count - 1);
        var margin = Z95 * Math.Sqrt(variance / values.Count);
        var (low, high) = clipToUnit ? (Math.Max(0, mean - margin), Math.Min(1, mean + margin)) : (mean - margin, mean + margin);
        return new ConfidenceInterval(mean, low, high);
    }

    /// <summary>
    /// The Shannon entropy, in bits, of a distribution given by counts: zero when one item takes everything,
    /// log2(n) when the n items are used evenly.
    /// </summary>
    public static double Entropy(IEnumerable<int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        var positive = counts.Where(count => count > 0).ToList();
        var total = (double)positive.Sum();
        return total == 0 ? 0 : -positive.Sum(count => count / total * Math.Log2(count / total));
    }
}
