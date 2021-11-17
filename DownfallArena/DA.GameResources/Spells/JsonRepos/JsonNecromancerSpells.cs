using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonNecromancerSpells : JsonBaseSpells, INecromancerSpells
    {
        public JsonNecromancerSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetSummonMinions()
        {
            return GetList().SingleOrDefault(x => x.Name == "Summon Minions");
        }

        public Spell GetRevenantGuards()
        {
            return GetList().SingleOrDefault(x => x.Name == "Revenant Guards");
        }

        public Spell GetCrazedSpecters()
        {
            return GetList().SingleOrDefault(x => x.Name == "Crazed Specters");
        }
    }
}
