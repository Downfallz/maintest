using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class ResolveNextActionHandler(MatchWorkflow workflow) : ICommandHandler<ResolveNextAction, Result<CombatStep>>
{
    public Task<Result<CombatStep>> HandleAsync(ResolveNextAction command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(command.MatchId, match => match.ResolveNextAction(), cancellationToken);
    }
}
