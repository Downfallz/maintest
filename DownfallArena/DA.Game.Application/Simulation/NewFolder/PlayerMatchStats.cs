using DA.Game.Shared.Contracts.Matches.Enums;

namespace DA.Game.Application.Simulation.NewFolder;

public sealed record PlayerMatchStats(
    PlayerSlot Slot,
    int CreaturesLost,
    int TotalDamageDealt,
    int TotalHealApplied,
    int TotalCrits,
    int TotalStunsApplied,
    int TotalEvolutions
);