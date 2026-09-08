using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class SubmitSpeedChoiceHandler(MatchWorkflow workflow) : ICommandHandler<SubmitSpeedChoice, Result>
{
    public Task<Result> HandleAsync(SubmitSpeedChoice command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.SubmitSpeedChoice(command.Slot, new SpeedChoice(command.Creature, command.Speed)),
            cancellationToken);
    }
}
