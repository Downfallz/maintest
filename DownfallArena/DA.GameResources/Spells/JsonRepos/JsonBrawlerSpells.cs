using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonBrawlerSpells : JsonBaseSpells, IBrawlerSpells
    {
        public JsonBrawlerSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetPummel()
        {
            return GetList().SingleOrDefault(x => x.Name == "Pummel");
        }

        public Spell GetGuard()
        {
            return GetList().SingleOrDefault(x => x.Name == "Guard");
        }
    }
}
