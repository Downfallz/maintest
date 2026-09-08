using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI.CharAction;

public record BestPick()
{
    public Spell Spell { get; set; }
    public List<Guid> BestTargets { get; set; }
    public double BestScore { get; set; }
}