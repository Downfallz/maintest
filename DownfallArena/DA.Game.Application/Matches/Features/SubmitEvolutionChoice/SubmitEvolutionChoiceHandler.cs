using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Messaging;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed class SubmitEvolutionChoiceHandler(IMatchRepository repo, 
    IApplicationEventCollector appEvents,
    IClock clock,
    IRandom rng) : IRequestHandler<SubmitEvolutionChoiceCommand, Result<SubmitEvolutionResult>>
{
    public async Task<Result<SubmitEvolutionResult>> Handle(SubmitEvolutionChoiceCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match is null)
            return Result<SubmitEvolutionResult>.Fail($"Match '{cmd.MatchId}' not found.");
        var res = match.SubmitEvolutionChoice(cmd.slot, cmd.SpellUnlockChoice, clock);
        var test = new SubmitEvolutionResult(match.CurrentRound.Player1Choices, match.CurrentRound.Phase)
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return res;
    }
}
