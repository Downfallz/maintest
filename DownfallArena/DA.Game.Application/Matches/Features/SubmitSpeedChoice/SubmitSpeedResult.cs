using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed record SubmitSpeedResult(HashSet<SpeedChoice> CurrentChoicesForPlayer, RoundPhase State) : ValueObject;
