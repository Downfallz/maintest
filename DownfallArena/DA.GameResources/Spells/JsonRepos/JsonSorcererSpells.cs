using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonSorcererSpells : JsonBaseSpells, ISorcererSpells
    {
        public JsonSorcererSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetLightningBolt()
        {
            return GetList().SingleOrDefault(x => x.Name == "Lightning Bolt");
        }

        public Spell GetRejuvenate()
        {
            return GetList().SingleOrDefault(x => x.Name == "Rejuvenate");
        }
    }
}
