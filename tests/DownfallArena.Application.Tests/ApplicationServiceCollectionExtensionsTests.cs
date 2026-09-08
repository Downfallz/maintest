using Microsoft.Extensions.DependencyInjection;

namespace DownfallArena.Application.Tests;

public sealed class ApplicationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApplication_registers_the_system_time_provider()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddApplication_does_not_override_an_existing_time_provider()
    {
        var services = new ServiceCollection();
        var custom = new FakeTimeProvider();
        services.AddSingleton<TimeProvider>(custom);

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(custom);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
    }
}
