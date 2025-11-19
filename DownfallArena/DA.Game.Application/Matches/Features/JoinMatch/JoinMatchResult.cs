using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed record JoinMatchResult(PlayerSlot Slot, MatchState State) : ValueObject;
