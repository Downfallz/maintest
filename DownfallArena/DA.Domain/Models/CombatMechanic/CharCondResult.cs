using System;

namespace DA.Game.Domain.Models.CombatMechanic;

public record CharCondResult
{
    public Guid TargetCharacterId { get; set; }
    public int TargetCharacterTeam { get; set; }
    public string TargetCharacterName { get; set; }
    public CharCondition CharCondition { get; set; }
}