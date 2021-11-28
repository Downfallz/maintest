using DA.Game.Domain.Models.TalentsManagement.Spells;
using System.Collections.Generic;

namespace DA.Game.Domain.Models.CombatMechanic;

public record AppliedEffectResult
{
    public AppliedEffect Effect { get; set; }
    public List<StatModifierResult> StatResults { get; set; }
    public List<CharCondAddResult> CharCondResults { get; set; }
}