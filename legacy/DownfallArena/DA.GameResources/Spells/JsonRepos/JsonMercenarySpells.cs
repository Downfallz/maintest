using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonMercenarySpells : JsonBaseSpells, IMercenarySpells
    {
        public JsonMercenarySpells(string fileName) : base(fileName)
        {
        }

        public Spell GetProtectiveSlam()
        {
            return GetList().SingleOrDefault(x => x.Name == "Protective Slam");
        }

        public Spell GetChainSlash()
        {
            return GetList().SingleOrDefault(x => x.Name == "Chain Slash");
        }

        public Spell GetThunderingSeal()
        {
            return GetList().SingleOrDefault(x => x.Name == "Thundering Seal");
        }
    }
}
