using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record SubmitCombatActionResult(CombatActionChoice CurrentChoiceForPlayer, RoundPhase State) : ValueObject;
