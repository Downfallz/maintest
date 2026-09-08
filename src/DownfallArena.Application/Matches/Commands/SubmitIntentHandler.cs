using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class SubmitIntentHandler(MatchWorkflow workflow) : ICommandHandler<SubmitIntent, Result>
{
    public Task<Result> HandleAsync(SubmitIntent command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.SubmitIntent(command.Slot, new CombatIntent(command.Actor, command.Spell)),
            cancellationToken);
    }
}
