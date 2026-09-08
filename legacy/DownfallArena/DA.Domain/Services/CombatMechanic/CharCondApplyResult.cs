using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.CombatMechanic;

public record CharCondApplyResult
{
    public StatModifierResult StatModifierResult { get; set; }
    public int RoundsLeft {  get; set; }
}