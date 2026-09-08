using System;

namespace DA.Game.Domain.Models.TalentsManagement.Spells;

public record StatModifierResult
{
    public StatModifier Effect { get; set; }

    public Guid TargetCharacterId { get; set; }
    public int TargetCharacterTeam { get; set; }
    public string TargetCharacterName{ get; set; }
    public double TargetModifier { get; set; }
    public string TargetModifierName { get; set; }
    public double TotalEffectiveValue { get; set; }
    public double PreEffectStatsValue { get; set; }
    public double PostEffectStatsValue { get; set; }
}