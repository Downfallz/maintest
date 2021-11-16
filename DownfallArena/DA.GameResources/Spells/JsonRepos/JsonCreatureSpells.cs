using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonCreatureSpells : JsonBaseSpells, ICreatureSpells
    {
        public JsonCreatureSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetWait()
        {
            return GetList().SingleOrDefault(x => x.Name == "Wait");
        }

        public Spell GetAttack()
        {
            return GetList().SingleOrDefault(x => x.Name == "Strike");
        }

        public Spell GetSuperAttack()
        {
            return GetList().SingleOrDefault(x => x.Name == "Heavy Strike");
        }
    }
}
