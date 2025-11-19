using DA.Game.Application.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.SubmitSpeedChoice;

public sealed record SubmitSpeedChoiceCommand(MatchId MatchId, PlayerSlot slot, SpeedChoiceDto SpeedChoice) : ICommand<Result<SubmitSpeedResult>>;
