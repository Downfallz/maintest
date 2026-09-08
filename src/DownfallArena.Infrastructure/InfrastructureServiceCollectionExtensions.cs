using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure adapters (repositories, game resource loaders, ...).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
