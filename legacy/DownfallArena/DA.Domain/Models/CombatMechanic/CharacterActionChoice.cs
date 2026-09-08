using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Models.CombatMechanic
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
