using DA.Game.Application.Matches.ReadModels;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.ResolveNextCombatAction;

public sealed record ResolveNextCombatActionResult(CombatStepOutcomeView stepOutcome) : ValueObject;
