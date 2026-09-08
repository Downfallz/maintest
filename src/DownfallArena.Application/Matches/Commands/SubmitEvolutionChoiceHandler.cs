using DownfallArena.Application.Messaging;
using DownfallArena.Domain.Matches.Rounds;
using DownfallArena.SharedKernel.Primitives;

namespace DownfallArena.Application.Matches.Commands;

public sealed class SubmitEvolutionChoiceHandler(MatchWorkflow workflow) : ICommandHandler<SubmitEvolutionChoice, Result>
{
    public Task<Result> HandleAsync(SubmitEvolutionChoice command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return workflow.ExecuteAsync(
            command.MatchId,
            match => match.SubmitEvolutionChoice(command.Slot, new EvolutionChoice(command.Creature, command.Spell)),
            cancellationToken);
    }
}
