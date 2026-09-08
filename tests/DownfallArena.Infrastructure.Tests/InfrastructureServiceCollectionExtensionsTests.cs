using DownfallArena.Application.Matches.Ports;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.Infrastructure.Tests.Resources;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_registers_the_in_memory_repository_and_a_seeded_random_source()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(randomSeed: 99).ShouldBeSameAs(services);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMatchRepository>().ShouldBeOfType<InMemoryMatchRepository>();
        provider.GetRequiredService<IMatchRepository>().ShouldBeSameAs(provider.GetRequiredService<IMatchRepository>());
        provider.GetRequiredService<IRandomSource>().ShouldBeOfType<SeededRandomSource>().Seed.ShouldBe(99);
    }

    [Fact]
    public void AddInfrastructure_without_a_seed_still_provides_a_random_source()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IRandomSource>().NextDouble().ShouldBeInRange(0.0, 1.0);
    }

    [Fact]
    public void AddGameResources_loads_the_consolidated_schema_on_first_use()
    {
        using var content = new ContentDirectory().WithValidContent();
        GameSchemaBuilder.Write(GameSchemaBuilder.Build(content.Path), content.Output);
        var services = new ServiceCollection();

        services.AddGameResources(Path.Combine(content.Output, GameSchemaJson.SchemaFileName)).ShouldBeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var resources = provider.GetRequiredService<IGameResources>();
        resources.Spells.Count.ShouldBe(2);
        resources.ShouldBeSameAs(provider.GetRequiredService<IGameResources>());
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => InfrastructureServiceCollectionExtensions.AddInfrastructure(null!));
        Should.Throw<ArgumentNullException>(() => InfrastructureServiceCollectionExtensions.AddGameResources(null!, "schema.json"));
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddGameResources(" "));
    }
}
