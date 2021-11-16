using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonWarlordSpells : JsonBaseSpells, IWarlordSpells
    {
        public JsonWarlordSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetFullPlate()
        {
            return GetList().SingleOrDefault(x => x.Name == "Full Plate");
        }

        public Spell GetCrushingStomp()
        {
            return GetList().SingleOrDefault(x => x.Name == "Crushing Stomp");
        }

        public Spell GetRestorativeGush()
        {
            return GetList().SingleOrDefault(x => x.Name == "Restorative Gush");
        }
    }
}
