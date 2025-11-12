using DA.Game.Application.Matches.Ports;
using DA.Game.Application.Shared.Abstractions;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared;
using MediatR;

namespace DA.Game.Application.Matches.Features.PlayTurn;
public sealed class PlayTurnHandler(IMatchRepository repo, IClock clock) : IRequestHandler<PlayTurnCommand, Result>
{
    public async Task<Result> Handle(PlayTurnCommand cmd, CancellationToken ct = default)
    {
        var match = await repo.GetAsync(cmd.MatchId, ct);
        if (match == null)
            return Result.Fail("Match inexistant.");

        // (Gameplay futur) : appliquer l’action ici si nécessaire.
        // Pour le squelette on avance juste le tour.
        var res = match.EndTurn(cmd.PlayerId, clock);
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, ct);

        return Result.Ok();
    }
}