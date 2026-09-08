using System.Linq;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells.JsonRepos
{
    public class JsonWizardSpells : JsonBaseSpells, IWizardSpells
    {
        public JsonWizardSpells(string fileName) : base(fileName)
        {
        }

        public Spell GetMeteor()
        {
            return GetList().SingleOrDefault(x => x.Name == "Meteor");
        }

        public Spell GetIceSpear()
        {
            return GetList().SingleOrDefault(x => x.Name == "Ice Spear");
        }

        public Spell GetEngulfingFlames()
        {
            return GetList().SingleOrDefault(x => x.Name == "Engulfing Flames");
        }
    }
}
