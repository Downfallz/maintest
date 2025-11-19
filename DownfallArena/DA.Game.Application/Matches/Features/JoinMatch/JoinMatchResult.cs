using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Application.Matches.Features.JoinMatch;

public sealed record JoinMatchResult(PlayerSlot Slot, MatchState State) : ValueObject;
