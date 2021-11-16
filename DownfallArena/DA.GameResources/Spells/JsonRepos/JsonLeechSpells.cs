using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System.Linq;

namespace DA.Game.Resources.Spells
{
    public class JsonLeechSpells : JsonBaseSpells, ILeechSpells
    {
        public JsonLeechSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetParasiteJab()
        {
            return GetList().SingleOrDefault(x => x.Name == "Parasite Jab");
        }

        public Spell GetHatefulSacrifice()
        {
            return GetList().SingleOrDefault(x => x.Name == "Hateful Sacrifice");
        }

        public Spell GetSoulDevourer()
        {
            return GetList().SingleOrDefault(x => x.Name == "Soul Devourer");
        }
    }
}
