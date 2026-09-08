using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class JoinMatchHandler(MatchWorkflow workflow) : ICommandHandler<JoinMatch, Result<PlayerSlot>>
{
    public Task<Result<PlayerSlot>> HandleAsync(JoinMatch command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(command.MatchId, match => match.Join(command.Player, command.Roster), cancellationToken);
    }
}
