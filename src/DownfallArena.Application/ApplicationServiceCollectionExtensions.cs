using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DownfallArena.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application-layer services (use case handlers, validators, ...).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
