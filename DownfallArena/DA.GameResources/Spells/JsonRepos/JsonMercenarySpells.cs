using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonMercenarySpells : JsonBaseSpells, IMercenarySpells
    {
        public JsonMercenarySpells(string fileName) : base(fileName)
        {
        }

        public Spell GetProtectiveSlam()
        {
            return GetList().SingleOrDefault(x => x.Name == "Protective Slam");
        }

        public Spell GetChainSlash()
        {
            return GetList().SingleOrDefault(x => x.Name == "Chain Slash");
        }

        public Spell GetThunderingSeal()
        {
            return GetList().SingleOrDefault(x => x.Name == "Thundering Seal");
        }
    }
}
