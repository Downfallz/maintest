using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.Commands.SubmitSpeedChoice;

public sealed record SubmitSpeedResult(HashSet<SpeedChoice> CurrentChoicesForPlayer, RoundPhase State) : ValueObject;
