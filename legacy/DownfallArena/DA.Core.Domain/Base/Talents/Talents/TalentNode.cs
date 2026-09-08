using System;

namespace DA.Core.Domain.Base.Talents.Talents
{
    [Serializable]
    public class TalentNode
    {
        public Spell Spell { get; set; }

        public bool IsUnlocked { get; set; }
    }
}
