using DownfallArena.Infrastructure.Randomness;

namespace DownfallArena.Infrastructure.Tests.Randomness;

public sealed class SeededRandomSourceTests
{
    [Fact]
    public void The_same_seed_replays_the_same_sequence()
    {
        var first = new SeededRandomSource(1234);
        var second = new SeededRandomSource(1234);

        var firstSequence = Enumerable.Range(0, 10).Select(_ => first.NextInt32(0, 100)).ToList();
        var secondSequence = Enumerable.Range(0, 10).Select(_ => second.NextInt32(0, 100)).ToList();

        firstSequence.ShouldBe(secondSequence);
        first.Seed.ShouldBe(1234);
        first.NextDouble().ShouldBe(second.NextDouble());
    }

    [Fact]
    public void Values_stay_within_the_requested_ranges()
    {
        var random = new SeededRandomSource(7);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            random.NextInt32(3, 5).ShouldBeInRange(3, 4);
            random.NextDouble().ShouldBeInRange(0.0, 0.999999999);
        }
    }

    [Fact]
    public void An_empty_range_is_rejected()
    {
        var random = new SeededRandomSource(7);

        Should.Throw<ArgumentOutOfRangeException>(() => random.NextInt32(5, 5));
        Should.Throw<ArgumentOutOfRangeException>(() => random.NextInt32(5, 4));
    }
}
