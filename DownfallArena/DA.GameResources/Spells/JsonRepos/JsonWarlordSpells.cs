using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
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
