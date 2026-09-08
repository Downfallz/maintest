using DA.Game.Domain2.Matches.ValueObjects.Evolution;
using DA.Game.Domain2.Shared.Messaging;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.Events.Planning;

public sealed record EvolutionChoiceSubmitted(RoundId RoundId, PlayerSlot Current, SpellUnlockChoice SpellUnlockChoice, DateTime dt) : EventBase(dt), IDomainEvent;
