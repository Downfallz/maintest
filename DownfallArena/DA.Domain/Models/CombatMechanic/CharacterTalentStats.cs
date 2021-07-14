using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System;
using System.Collections.Generic;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class CharacterTalentStats
    {
        public List<PassiveEffect> PassiveEffects { get; set; }
        public IReadOnlyList<Spell> UnlockedSpells { get; set; }
        public IReadOnlyList<Spell> UnlockableSpells { get; set; }
        public int Initiative { get; set; }
    }
}
