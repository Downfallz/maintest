using DA.Game.Application.Matches.DTOs;
using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.ResolveNextCombatAction;

public sealed record ResolveNextCombatActionCommand(MatchId MatchId) : ICommand<Result<ResolveNextCombatActionResult>>;
