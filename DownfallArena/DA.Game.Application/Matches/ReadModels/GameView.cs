using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Application.Matches.ReadModels;
public sealed record GameView(
    MatchId MatchId,
    PlayerSlot? CurrentTurn,
    int TurnNumber,
    PlayerId? Player1Id,
    PlayerId? Player2Id);

