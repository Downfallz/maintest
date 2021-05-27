using System;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class SpellUnlockChoice
    {
        public Guid CharacterId { get; set; }
        public Spell Spell { get; set; }
    }
}
