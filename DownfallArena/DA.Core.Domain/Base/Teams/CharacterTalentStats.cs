using System;
using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents;

namespace DA.Core.Domain.Base.Teams
{
    [Serializable]
    public class CharacterTalentStats
    {
        public List<Spell> Spells { get; set; }
        public List<PassiveEffect> PassiveEffects { get; set; }

        public IReadOnlyList<Spell> UnlockedTalents { get; set; }

        public IReadOnlyList<Spell> UnlockableTalents { get; set; }
        public int Initiative { get; set; }
    }
}
