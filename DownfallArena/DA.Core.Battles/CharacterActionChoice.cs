using System;
using System.Collections.Generic;
using System.Text;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Domain.Battles
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
