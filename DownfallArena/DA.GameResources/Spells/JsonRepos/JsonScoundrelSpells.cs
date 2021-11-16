using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonScoundrelSpells : JsonBaseSpells, IScoundrelSpells
    {
        public JsonScoundrelSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetPoisonSlash()
        {
            return GetList().SingleOrDefault(x => x.Name == "Poison Slash");
        }

        public Spell GetThrowingStar()
        {
            return GetList().SingleOrDefault(x => x.Name == "Throwing Star");
        }
    }
}
