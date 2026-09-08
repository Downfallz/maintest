using DownfallArena.Application.Evaluation;

namespace DownfallArena.Application.Tests.Evaluation;

public sealed class PairedStatisticsTests
{
    [Fact]
    public void The_interval_is_the_mean_plus_or_minus_the_standard_error()
    {
        var interval = PairedStatistics.Interval([1, 0, 1, 0], clipToUnit: false);

        interval.Mean.ShouldBe(0.5);
        var margin = PairedStatistics.Z95 * Math.Sqrt(1.0 / 3 / 4);
        interval.Low.ShouldBe(0.5 - margin, 1e-9);
        interval.High.ShouldBe(0.5 + margin, 1e-9);
    }

    [Fact]
    public void Rates_are_clipped_to_the_unit_interval()
    {
        var interval = PairedStatistics.Interval([1, 0, 1, 0]);

        interval.Low.ShouldBe(0);
        interval.High.ShouldBe(1);
    }

    [Fact]
    public void Perfectly_correlated_pairs_widen_the_interval_compared_with_counting_matches_twice()
    {
        // Four seeds whose two mirrored matches always agree: as pairs, four values; as matches, eight.
        var pairs = PairedStatistics.Interval([1, 0, 1, 0], clipToUnit: false);
        var matches = PairedStatistics.Interval([1, 1, 0, 0, 1, 1, 0, 0], clipToUnit: false);

        pairs.Mean.ShouldBe(matches.Mean);
        (pairs.High - pairs.Low).ShouldBeGreaterThan(matches.High - matches.Low);
    }

    [Fact]
    public void Degenerate_inputs_give_degenerate_intervals()
    {
        PairedStatistics.Interval([]).ShouldBe(new ConfidenceInterval(0, 0, 0));
        PairedStatistics.Interval([0.7]).ShouldBe(new ConfidenceInterval(0.7, 0.7, 0.7));
        PairedStatistics.Interval([0.4, 0.4, 0.4]).ShouldBe(new ConfidenceInterval(0.4, 0.4, 0.4));
        Should.Throw<ArgumentNullException>(() => PairedStatistics.Interval(null!));
    }

    [Fact]
    public void Entropy_is_zero_for_one_spell_and_log2_n_for_n_even_spells()
    {
        PairedStatistics.Entropy([10]).ShouldBe(0);
        PairedStatistics.Entropy([10, 0]).ShouldBe(0);
        PairedStatistics.Entropy([10, 10]).ShouldBe(1, 1e-9);
        PairedStatistics.Entropy([5, 5, 5, 5]).ShouldBe(2, 1e-9);
        PairedStatistics.Entropy([]).ShouldBe(0);
        PairedStatistics.Entropy([90, 10]).ShouldBeLessThan(1);
        Should.Throw<ArgumentNullException>(() => PairedStatistics.Entropy(null!));
    }
}
