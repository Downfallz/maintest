using System;
using System.Collections.Generic;
using System.IO;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.TalentsManagement.Tools;

namespace DA.Game.Resources.Generator
{
    public class GenerateList
    {
        private IGetSpell _getSpell;

        public GenerateList(IGetSpell getSpell)
        {
            _getSpell = getSpell;
        }

        public void Generate()
        {
            List<Spell> spells = new List<Spell>();

            using (var w = new StreamWriter($@"C:\lol\lol.csv"))
            {
                foreach (TalentList tal in (TalentList[])Enum.GetValues(typeof(TalentList)))
                {
                    var spell = _getSpell.FromEnum(tal);
                    var line = $"{spell.Name},{spell.CharacterClass},{spell.SpellType},{spell.EnergyCost},{spell.MinionsCost},{spell.Initiative},{spell.NbTargets},{spell.CriticalChance}";
                    w.WriteLine(line);
                    w.Flush();
                }
            }
        }
    }
}
