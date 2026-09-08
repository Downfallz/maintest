using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class PassEvolutionHandler(MatchWorkflow workflow) : ICommandHandler<PassEvolution, Result>
{
    public Task<Result> HandleAsync(PassEvolution command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(command.MatchId, match => match.PassEvolution(command.Slot), cancellationToken);
    }
}
