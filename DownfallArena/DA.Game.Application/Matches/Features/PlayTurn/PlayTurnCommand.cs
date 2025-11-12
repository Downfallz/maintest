using DA.Game.Application.Shared.Primitives;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Players.Ids;
using DA.Game.Shared;

namespace DA.Game.Application.Matches.Features.PlayTurn;
public sealed record PlayTurnCommand(MatchId MatchId, PlayerId PlayerId, PlayerAction Action) : ICommand<Result>;