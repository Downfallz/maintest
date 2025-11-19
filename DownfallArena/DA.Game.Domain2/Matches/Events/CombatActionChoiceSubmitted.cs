using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events;

public sealed record CombatActionChoiceSubmitted(RoundId RoundId, PlayerSlot Current, CombatActionChoice SpeedChoice, DateTime dt) : EventBase(dt), IDomainEvent;
