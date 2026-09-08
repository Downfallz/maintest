using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonSorcererSpells : JsonBaseSpells, ISorcererSpells
    {
        public JsonSorcererSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetLightningBolt()
        {
            return GetList().SingleOrDefault(x => x.Name == "Lightning Bolt");
        }

        public Spell GetRejuvenate()
        {
            return GetList().SingleOrDefault(x => x.Name == "Rejuvenate");
        }
    }
}
