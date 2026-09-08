using DownfallArena.Application.Agents;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Ports;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Ports;
using DownfallArena.Application.Simulation;
using DownfallArena.Application.Tests.Support;
using DownfallArena.Domain.Matches;
using DownfallArena.Domain.Resources;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using DownfallArena.SharedKernel.Randomness;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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

    [Fact]
    public void AddApplication_registers_every_use_case_the_driver_and_the_agents_over_the_ports()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IMatchRepository>());
        services.AddSingleton<IGameResources>(TestContent.Resources);
        services.AddSingleton<IRandomSource>(new TestRandom(1));
        services.AddSingleton<IRandomSourceFactory>(new TestRandomFactory());

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDomainEventDispatcher>().ShouldBeOfType<DomainEventDispatcher>();
        provider.GetRequiredService<ICommandHandler<CreateMatch, Result<MatchId>>>().ShouldBeOfType<CreateMatchHandler>();
        provider.GetRequiredService<ICommandHandler<JoinMatch, Result<PlayerSlot>>>().ShouldBeOfType<JoinMatchHandler>();
        provider.GetRequiredService<ICommandHandler<SubmitEvolutionChoice, Result>>().ShouldBeOfType<SubmitEvolutionChoiceHandler>();
        provider.GetRequiredService<ICommandHandler<PassEvolution, Result>>().ShouldBeOfType<PassEvolutionHandler>();
        provider.GetRequiredService<ICommandHandler<SubmitSpeedChoice, Result>>().ShouldBeOfType<SubmitSpeedChoiceHandler>();
        provider.GetRequiredService<ICommandHandler<SubmitIntent, Result>>().ShouldBeOfType<SubmitIntentHandler>();
        provider.GetRequiredService<ICommandHandler<SubmitAction, Result>>().ShouldBeOfType<SubmitActionHandler>();
        provider.GetRequiredService<ICommandHandler<ResolveNextAction, Result<CombatStep>>>().ShouldBeOfType<ResolveNextActionHandler>();
        provider.GetRequiredService<IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>>>().ShouldBeOfType<GetBoardStateForPlayerHandler>();
        provider.GetRequiredService<IQueryHandler<GetPlayerOptions, Result<PlayerOptions>>>().ShouldBeOfType<GetPlayerOptionsHandler>();
        provider.GetRequiredService<MatchDriver>().ShouldNotBeNull();
        provider.GetRequiredService<RandomAgent>().ShouldNotBeNull();
        provider.GetRequiredService<BatchRunner>().ShouldNotBeNull();
    }

    [Fact]
    public void AddApplication_rejects_a_null_service_collection()
    {
        Should.Throw<ArgumentNullException>(() => ApplicationServiceCollectionExtensions.AddApplication(null!));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
    }
}
