using DA.Game.Domain2.Matches.ValueObjects.SpeedVo;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed record SubmitSpeedResult(HashSet<SpeedChoice> CurrentChoicesForPlayer, RoundPhase State) : ValueObject;
