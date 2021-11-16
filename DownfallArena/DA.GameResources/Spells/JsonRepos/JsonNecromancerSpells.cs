using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonNecromancerSpells : JsonBaseSpells, INecromancerSpells
    {
        public JsonNecromancerSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetSummonMinions()
        {
            return GetList().SingleOrDefault(x => x.Name == "Summon Minions");
        }

        public Spell GetRevenantGuards()
        {
            return GetList().SingleOrDefault(x => x.Name == "Revenant Guards");
        }

        public Spell GetCrazedSpecters()
        {
            return GetList().SingleOrDefault(x => x.Name == "Crazed Specters");
        }
    }
}
