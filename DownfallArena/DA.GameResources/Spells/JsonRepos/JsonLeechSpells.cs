using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
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
