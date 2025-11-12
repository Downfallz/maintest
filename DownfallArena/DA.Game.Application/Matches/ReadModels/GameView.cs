using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Players.Ids;

namespace DA.Game.Domain2.Match.ReadModels;
public sealed record GameView(
    MatchId MatchId,
    PlayerSlot? CurrentTurn,
    int TurnNumber,
    PlayerId? Player1Id,
    PlayerId? Player2Id);

