using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.SharedKernel.Tests.Identifiers;

public sealed class SequentialIdentifierTests
{
    [Fact]
    public void The_first_round_is_number_one()
    {
        RoundId.First.Number.ShouldBe(1);
        RoundId.First.ToString().ShouldBe("1");
    }

    [Fact]
    public void The_next_round_increments_the_number()
    {
        RoundId.First.Next().ShouldBe(RoundId.From(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_round_number_must_be_positive(int number)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RoundId.From(number));
    }

    [Fact]
    public void Creature_ids_with_the_same_value_are_equal()
    {
        CreatureId.From(3).ShouldBe(CreatureId.From(3));
        CreatureId.From(3).ShouldNotBe(CreatureId.From(4));
        CreatureId.From(3).ToString().ShouldBe("3");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_creature_id_must_be_positive(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreatureId.From(value));
    }
}
