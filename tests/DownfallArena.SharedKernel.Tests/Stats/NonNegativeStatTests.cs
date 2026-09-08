using DownfallArena.SharedKernel.Stats;

namespace DownfallArena.SharedKernel.Tests.Stats;

public sealed class NonNegativeStatTests
{
    [Fact]
    public void A_stat_stores_its_value()
    {
        Health.Of(20).Value.ShouldBe(20);
        Energy.Of(3).Value.ShouldBe(3);
        Defense.Of(2).Value.ShouldBe(2);
        Initiative.Of(5).Value.ShouldBe(5);
    }

    [Fact]
    public void A_stat_cannot_be_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Health.Of(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Energy.Of(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Defense.Of(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Initiative.Of(-1));
    }

    [Fact]
    public void Adding_returns_a_new_value_and_keeps_the_original()
    {
        var health = Health.Of(10);

        var healed = health.Plus(5);

        healed.ShouldBe(Health.Of(15));
        health.ShouldBe(Health.Of(10));
    }

    [Fact]
    public void Subtracting_floors_at_zero()
    {
        Health.Of(3).Minus(5).ShouldBe(Health.Of(0));
        Health.Of(3).Minus(5).IsZero.ShouldBeTrue();
        Health.Of(3).Minus(2).IsZero.ShouldBeFalse();
    }

    [Fact]
    public void Amounts_must_be_non_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Energy.Of(3).Plus(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Energy.Of(3).Minus(-1));
    }

    [Fact]
    public void Stats_compare_by_value()
    {
        (Energy.Of(2) < Energy.Of(3)).ShouldBeTrue();
        (Energy.Of(3) > Energy.Of(2)).ShouldBeTrue();
        (Energy.Of(2) <= Energy.Of(2)).ShouldBeTrue();
        (Energy.Of(2) >= Energy.Of(3)).ShouldBeFalse();
        Energy.Of(2).CompareTo(Energy.Of(2)).ShouldBe(0);
        Energy.Of(2).CompareTo(null).ShouldBePositive();
    }

    [Fact]
    public void Stats_of_different_kinds_are_not_equal_even_with_the_same_value()
    {
        object health = Health.Of(2);
        object defense = Defense.Of(2);

        health.Equals(defense).ShouldBeFalse();
    }

    [Fact]
    public void A_stat_renders_as_its_value()
    {
        Initiative.Of(7).ToString().ShouldBe("7");
    }
}
