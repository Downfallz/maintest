using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonShamanSpells : JsonBaseSpells, IShamanSpells
    {
        public JsonShamanSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetHealingScreech()
        {
            return GetList().SingleOrDefault(x => x.Name == "Healing Screech");
        }

        public Spell GetToxicWaves()
        {
            return GetList().SingleOrDefault(x => x.Name == "Toxic Waves");
        }

        public Spell GetRestoringBurst()
        {
            return GetList().SingleOrDefault(x => x.Name == "Restoring Burst");
        }
    }
}
