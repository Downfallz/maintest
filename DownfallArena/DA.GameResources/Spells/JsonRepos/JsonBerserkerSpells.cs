using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
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
