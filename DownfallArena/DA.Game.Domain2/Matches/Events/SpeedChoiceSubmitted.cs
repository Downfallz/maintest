using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Match.ValueObjects;
using DA.Game.Domain2.Matches.Ids;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared;

namespace DA.Game.Domain2.Match.Events;

public sealed record SpeedChoiceSubmitted(RoundId RoundId, PlayerSlot Current, SpeedChoice SpeedChoice, DateTime dt) : EventBase(dt), IDomainEvent;
