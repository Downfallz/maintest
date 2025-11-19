using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.CreateMatch;

public sealed record CreateMatchCommand() : ICommand<Result<MatchId>>;
