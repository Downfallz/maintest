using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonBerserkerSpells : JsonBaseSpells, IBerserkerSpells
    {
        public JsonBerserkerSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetEnragedCharge()
        {
            return GetList().SingleOrDefault(x => x.Name == "Enraged Charge");
        }

        public Spell GetTornado()
        {
            return GetList().SingleOrDefault(x => x.Name == "Tornado");
        }

        public Spell GetPsychoRush()
        {
            return GetList().SingleOrDefault(x => x.Name == "Psycho Rush");
        }
    }
}
