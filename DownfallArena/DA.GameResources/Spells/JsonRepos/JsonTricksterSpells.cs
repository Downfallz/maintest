using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonTricksterSpells : JsonBaseSpells, ITricksterSpells
    {
        public JsonTricksterSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetNoxiousCure()
        {
            return GetList().SingleOrDefault(x => x.Name == "Noxious Cure");
        }

        public Spell GetTranquilizerDart()
        {
            return GetList().SingleOrDefault(x => x.Name == "Tranquilizer Dart");
        }

        public Spell GetInfectiousBlast()
        {
            return GetList().SingleOrDefault(x => x.Name == "Infectious Blast");
        }
    }
}
