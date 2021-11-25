using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services.CombatMechanic;

namespace DA.Game.Domain.Models.CombatMechanic;

public record AppliedEffectResult
{
    public AppliedEffect Effect { get; set; }
    public List<StatModifierResult> StatResults { get; set; }
    public List<CharCondResult> CharCondResults { get; set; }
}