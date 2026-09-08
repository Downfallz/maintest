using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonAssassinSpells : JsonBaseSpells, IAssassinSpells
    {
        public JsonAssassinSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetDeathSquad()
        {
            return GetList().SingleOrDefault(x => x.Name == "Death Squad");
        }

        public Spell GetMomentum()
        {
            return GetList().SingleOrDefault(x => x.Name == "Momentum");
        }

        public Spell GetMortalWound()
        {
            return GetList().SingleOrDefault(x => x.Name == "Mortal Wound");
         
        }
    }
}
