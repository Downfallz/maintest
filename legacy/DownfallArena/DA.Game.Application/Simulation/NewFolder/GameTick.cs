using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed record GameTick(
    int Index,
    GameTickKind Kind,
    PlayerSlot? ActorSlot,
    CreatureId? ActorId,
    object Payload,           // decision or outcome
    DateTimeOffset AtUtc
);
