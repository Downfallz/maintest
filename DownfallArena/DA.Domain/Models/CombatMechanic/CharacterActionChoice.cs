using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class CharacterActionChoice
    {
        public CharacterActionChoice()
        {
            Targets = new List<Guid>();
        }
        public Guid CharacterId { get; set; }
        public Spell Spell { get; set; }
        public List<Guid> Targets { get; set; }
    }
}
