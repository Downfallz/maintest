using DownfallArena.Infrastructure.Randomness;

namespace DownfallArena.Infrastructure.Tests.Randomness;

public sealed class SeededRandomSourceFactoryTests
{
    [Fact]
    public void The_factory_creates_a_source_seeded_as_asked()
    {
        var factory = new SeededRandomSourceFactory();

        var created = factory.Create(2026).ShouldBeOfType<SeededRandomSource>();

        created.Seed.ShouldBe(2026);
        created.NextInt32(0, 1000).ShouldBe(new SeededRandomSource(2026).NextInt32(0, 1000));
    }
}
