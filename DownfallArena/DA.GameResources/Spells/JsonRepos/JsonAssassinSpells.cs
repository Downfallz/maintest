using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using System.Linq;

namespace DA.Game.Resources.Spells
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
