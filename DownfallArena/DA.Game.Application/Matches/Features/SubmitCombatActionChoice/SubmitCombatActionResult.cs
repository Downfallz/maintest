using DA.Game.Application.Matches.DTOs;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Application.Matches.Features.SubmitCombatActionChoice;

public sealed record SubmitCombatActionResult(CombatActionChoiceDto CurrentChoiceForPlayer, RoundPhase State) : ValueObject;
