using AutoMapper;
using DA.Game.Application.Matches.Ports;
using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.SubmitEvolutionChoice;

public sealed class SubmitEvolutionChoiceHandler(IMatchRepository repo,
    IMapper mapper) : IRequestHandler<SubmitEvolutionChoiceCommand, Result<SubmitEvolutionResult>>
{
    public async Task<Result<SubmitEvolutionResult>> Handle(SubmitEvolutionChoiceCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match is null)
            return Result<SubmitEvolutionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var domainChoice = mapper.Map<SpellUnlockChoice>(cmd.SpellUnlockChoice);
        var res = match.SubmitEvolutionChoice(cmd.slot, domainChoice);

        if (!res.IsSuccess)
            return Result<SubmitEvolutionResult>.Fail(res.Error!);

        await repo.SaveAsync(match, cancellationToken);

        var choices = cmd.slot == PlayerSlot.Player1 ? match.CurrentRound!.Player1Choices : match.CurrentRound!.Player2Choices;

        return Result<SubmitEvolutionResult>.Ok(new SubmitEvolutionResult(choices.ToHashSet(), match.CurrentRound.Phase));
    }
}
