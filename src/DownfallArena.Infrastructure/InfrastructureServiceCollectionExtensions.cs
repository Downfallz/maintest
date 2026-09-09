using DownfallArena.Application.Agents.Ports;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Ports;
using DownfallArena.Domain.Resources;
using DownfallArena.Infrastructure.Agents;
using DownfallArena.Infrastructure.Matches;
using DownfallArena.Infrastructure.Randomness;
using DownfallArena.Infrastructure.Resources;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DownfallArena.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the adapters: the in-memory match repository, the seeded random source factory, the weights
    /// files of the heuristic agent, and a process random source. Without a seed the process source is seeded once per process; pass one to replay.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMatchRepository, InMemoryMatchRepository>();
        services.TryAddSingleton<IRandomSourceFactory, SeededRandomSourceFactory>();
        services.TryAddSingleton<IScoringWeightsSource, JsonScoringWeightsSource>();
        services.TryAddSingleton<IPolicySource, JsonPolicySource>();
        services.TryAddSingleton<IRandomSource>(_ => new SeededRandomSource(randomSeed ?? Random.Shared.Next()));

        return services;
    }

    /// <summary>
    /// Registers the game resources loaded from a consolidated <c>game.schema.json</c> (ADR 0009). The file is
    /// read once, when the resources are first resolved.
    /// </summary>
    public static IServiceCollection AddGameResources(this IServiceCollection services, string schemaPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);

        services.TryAddSingleton<IGameResources>(_ => GameSchemaBuilder.Load(schemaPath));

        return services;
    }
}
