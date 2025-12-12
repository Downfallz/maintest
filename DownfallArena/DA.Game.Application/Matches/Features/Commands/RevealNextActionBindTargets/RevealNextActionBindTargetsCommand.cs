using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.RevealNextActionBindTargets;

public sealed record RevealNextActionBindTargetsCommand(MatchId MatchId, CreatureId sourceId, IReadOnlyList<CreatureId> TargetIds) : ICommand<Result<RevealNextActionBindTargetsResult>>;
