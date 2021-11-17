using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonBrawlerSpells : JsonBaseSpells, IBrawlerSpells
    {
        public JsonBrawlerSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetPummel()
        {
            return GetList().SingleOrDefault(x => x.Name == "Pummel");
        }

        public Spell GetGuard()
        {
            return GetList().SingleOrDefault(x => x.Name == "Guard");
        }
    }
}
