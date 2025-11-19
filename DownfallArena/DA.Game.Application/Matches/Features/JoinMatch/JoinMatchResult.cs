using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.ValueObjects;

public sealed record JoinMatchResult(PlayerSlot Slot, MatchState State) : ValueObject;
