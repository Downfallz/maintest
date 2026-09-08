using System;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.CombatMechanic
{
    [Serializable]
    public class SpellUnlockChoice
    {
        public Guid CharacterId { get; set; }
        public Spell Spell { get; set; }
    }
}
