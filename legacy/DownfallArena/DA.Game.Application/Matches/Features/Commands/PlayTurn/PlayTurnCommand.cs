using DA.Game.Application.Shared.Primitives;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Players.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.PlayTurn;

public sealed record PlayTurnCommand(MatchId MatchId, PlayerId PlayerId, PlayerAction Action) : ICommand<Result>;