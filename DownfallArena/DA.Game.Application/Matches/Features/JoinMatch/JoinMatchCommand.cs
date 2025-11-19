using DA.Game.Application.Shared.Primitives;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed record JoinMatchCommand(MatchId MatchId, PlayerRef PlayerRef) : ICommand<Result<JoinMatchResult>>;
