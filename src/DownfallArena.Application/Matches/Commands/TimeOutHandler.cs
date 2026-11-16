using DownfallArena.Application.Messaging;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class TimeOutHandler(MatchWorkflow workflow) : ICommandHandler<TimeOut, Result>
{
    public Task<Result> HandleAsync(TimeOut command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.TimeOut([.. command.Late]),
            cancellationToken);
    }
}
