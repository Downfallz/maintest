using DA.Game.Application.Matches.Ports;
using DA.Game.Shared.Utilities;
using MediatR;

namespace DA.Game.Application.Matches.Features.Commands.PlayTurn;

public sealed class PlayTurnHandler(IMatchRepository repo) : IRequestHandler<PlayTurnCommand, Result>
{
    public async Task<Result> Handle(PlayTurnCommand cmd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var match = await repo.GetAsync(cmd.MatchId, cancellationToken);
        if (match == null)
            return Result.Fail("Match inexistant.");

        // (Gameplay futur) : appliquer l’action ici si nécessaire.
        // Pour le squelette on avance juste le tour.
        var res = match.EndTurn(cmd.PlayerId);
        if (!res.IsSuccess) return res;

        await repo.SaveAsync(match, cancellationToken);

        return Result.Ok();
    }
}