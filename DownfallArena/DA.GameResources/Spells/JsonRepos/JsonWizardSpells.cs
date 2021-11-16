using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
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
