using System;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.TalentsManagement
{
    [Serializable]
    public class TalentNode
    {
        public Spell Spell { get; set; }

        public bool IsUnlocked { get; set; }
    }
}
