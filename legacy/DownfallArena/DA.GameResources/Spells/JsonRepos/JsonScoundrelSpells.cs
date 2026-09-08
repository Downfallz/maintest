using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
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
