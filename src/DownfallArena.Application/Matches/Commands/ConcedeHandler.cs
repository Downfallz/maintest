using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class ConcedeHandler(MatchWorkflow workflow) : ICommandHandler<Concede, Result>
{
    public Task<Result> HandleAsync(Concede command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.Concede(command.Slot),
            cancellationToken);
    }
}
