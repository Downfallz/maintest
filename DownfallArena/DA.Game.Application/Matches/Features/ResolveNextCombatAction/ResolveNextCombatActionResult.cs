using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed record ResolveNextCombatActionResult(bool RoundHasEnded) : ValueObject;
