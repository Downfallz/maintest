using DA.Game.Domain2.Matches.ValueObjects.Planning;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.Planning;

public sealed record SpeedChoiceSubmitted(RoundId RoundId, PlayerSlot Current, SpeedChoice SpeedChoice, DateTime dt) : EventBase(dt), IDomainEvent;
