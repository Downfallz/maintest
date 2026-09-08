using DownfallArena.Application.Agents;
using DownfallArena.Application.Evaluation;
using DownfallArena.Application.Matches;
using DownfallArena.Application.Matches.Commands;
using DownfallArena.Application.Matches.Driving;
using DownfallArena.Application.Matches.Projections;
using DownfallArena.Application.Matches.Queries;
using DownfallArena.Application.Messaging;
using DownfallArena.Application.Simulation;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;
using DownfallArena.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DownfallArena.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the use cases, the projections' handlers, the event dispatcher, the agents, and the driver.
    /// The ports (<c>IMatchRepository</c>, <c>IGameResources</c>, <c>IRandomSource</c>, <c>IRandomSourceFactory</c>) come
    /// from Infrastructure.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        services.TryAddTransient<MatchWorkflow>();

        services.TryAddTransient<ICommandHandler<CreateMatch, Result<MatchId>>, CreateMatchHandler>();
        services.TryAddTransient<ICommandHandler<JoinMatch, Result<PlayerSlot>>, JoinMatchHandler>();
        services.TryAddTransient<ICommandHandler<SubmitEvolutionChoice, Result>, SubmitEvolutionChoiceHandler>();
        services.TryAddTransient<ICommandHandler<PassEvolution, Result>, PassEvolutionHandler>();
        services.TryAddTransient<ICommandHandler<SubmitSpeedChoice, Result>, SubmitSpeedChoiceHandler>();
        services.TryAddTransient<ICommandHandler<SubmitIntent, Result>, SubmitIntentHandler>();
        services.TryAddTransient<ICommandHandler<SubmitAction, Result>, SubmitActionHandler>();
        services.TryAddTransient<ICommandHandler<ResolveNextAction, Result<CombatStep>>, ResolveNextActionHandler>();
        services.TryAddTransient<IQueryHandler<GetBoardStateForPlayer, Result<PlayerBoardState>>, GetBoardStateForPlayerHandler>();
        services.TryAddTransient<IQueryHandler<GetPlayerOptions, Result<PlayerOptions>>, GetPlayerOptionsHandler>();

        services.TryAddTransient<MatchCommandHandlers>();
        services.TryAddTransient<MatchQueryHandlers>();
        services.TryAddTransient<MatchDriver>();
        services.TryAddTransient<RandomAgent>();
        services.TryAddSingleton<AgentFactory>();
        services.TryAddTransient<BatchRunner>();
        services.TryAddSingleton<CombatStatsRecorder>();
        services.TryAddTransient<EvaluationRunner>();

        return services;
    }
}
