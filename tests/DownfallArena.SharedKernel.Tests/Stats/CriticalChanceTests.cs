using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.SharedKernel.Tests.Stats;

public sealed class CriticalChanceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(1)]
    public void A_chance_between_zero_and_one_is_accepted(double value)
    {
        CriticalChance.Of(value).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void A_chance_outside_zero_and_one_is_rejected(double value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CriticalChance.Of(value));
    }

    [Fact]
    public void Arithmetic_clamps_to_the_valid_range()
    {
        CriticalChance.Of(0.9).Plus(0.5).ShouldBe(CriticalChance.Of(1));
        CriticalChance.Of(0.1).Minus(0.5).ShouldBe(CriticalChance.None);
        CriticalChance.Of(0.1).Plus(0.2).Value.ShouldBe(0.3, tolerance: 1e-9);
    }

    [Fact]
    public void A_chance_renders_as_a_percentage()
    {
        CriticalChance.Of(0.125).AsPercent.ShouldBe(12.5);
        CriticalChance.Of(0.125).ToString().ShouldBe("12.5%");
        CriticalChance.None.ToString().ShouldBe("0%");
    }
}
