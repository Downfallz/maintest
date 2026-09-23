using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class SubmitTieOrderHandler(MatchWorkflow workflow) : ICommandHandler<SubmitTieOrder, Result>
{
    public Task<Result> HandleAsync(SubmitTieOrder command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.SubmitTieOrder(command.Slot, command.Order),
            cancellationToken);
    }
}
