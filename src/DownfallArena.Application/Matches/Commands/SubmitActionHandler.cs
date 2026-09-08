using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class SubmitActionHandler(MatchWorkflow workflow) : ICommandHandler<SubmitAction, Result>
{
    public Task<Result> HandleAsync(SubmitAction command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.SubmitAction(command.Slot, CombatAction.Bind(new CombatIntent(command.Actor, command.Spell), command.Targets)),
            cancellationToken);
    }
}
