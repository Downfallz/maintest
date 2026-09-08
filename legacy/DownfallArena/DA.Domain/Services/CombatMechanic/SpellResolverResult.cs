using System.Collections.Generic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.CombatMechanic;

public record SpellResolverResult
{
    public LightCharInfo SourceCharInfo { get; set; }
    public List<LightCharInfo> TargetsCharInfo { get; set; }
    public bool IsCritical { get; set; }
    public Speed Speed { get; set; }
    public List<AppliedEffectResult> AppliedEffectResults { get; set; }
    public Spell Spell { get; set; }
    public int InitialEnergy { get; set; }
    public int InitialExtraPoint { get; set; }
    public int PostEnergy { get; set; }
    public int PostExtraPoint { get; set; }
}