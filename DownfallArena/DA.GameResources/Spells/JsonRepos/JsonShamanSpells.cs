using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Resources.Spells
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
