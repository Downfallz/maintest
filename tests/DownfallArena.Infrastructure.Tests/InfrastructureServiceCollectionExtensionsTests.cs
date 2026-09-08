using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure().ShouldBeSameAs(services);
    }

    [Fact]
    public void AddInfrastructure_rejects_a_null_service_collection()
    {
        Should.Throw<ArgumentNullException>(() => InfrastructureServiceCollectionExtensions.AddInfrastructure(null!));
    }
}
