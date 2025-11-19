using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed record JoinMatchCommand(MatchId MatchId, PlayerRef PlayerRef) : ICommand<Result<JoinMatchResult>>;
