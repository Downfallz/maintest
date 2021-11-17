using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonCreatureSpells : JsonBaseSpells, ICreatureSpells
    {
        public JsonCreatureSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetWait()
        {
            return GetList().SingleOrDefault(x => x.Name == "Wait");
        }

        public Spell GetAttack()
        {
            return GetList().SingleOrDefault(x => x.Name == "Strike");
        }

        public Spell GetSuperAttack()
        {
            return GetList().SingleOrDefault(x => x.Name == "Heavy Strike");
        }
    }
}
