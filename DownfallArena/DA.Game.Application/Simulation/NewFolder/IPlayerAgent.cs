using DA.Game.Application.Matches.ReadModels;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources;
using MediatR;

namespace DA.Game.Application.Simulation.NewFolder;

public interface IPlayerAgent
{
    // Agent chooses what to do when options are available.
    Task DecideEvolutionAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct);
    Task DecideSpeedAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct);
    Task DecideCombatIntentAsync(AgentCtx ctx, PlayerOptionsView opt, CancellationToken ct);
    Task DecideRevealForActorAsync(
           AgentCtx ctx,
           PlayerOptionsView playerAction,
           CreatureId actorId,
           CancellationToken ct);
}
