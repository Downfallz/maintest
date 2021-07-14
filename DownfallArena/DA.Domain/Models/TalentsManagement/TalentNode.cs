using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System;

namespace DA.Game.Domain.Models.GameFlowEngine.TalentsManagement
{
    [Serializable]
    public class TalentNode
    {
        public Spell Spell { get; set; }

        public bool IsUnlocked { get; set; }
    }
}
