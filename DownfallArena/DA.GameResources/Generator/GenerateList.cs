using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.TalentsManagement.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace DA.Game.Resources.Generator
{
    public class GenerateList
    {
        private readonly IGetSpell _getSpell;

        public GenerateList(IGetSpell getSpell)
        {
            _getSpell = getSpell;
        }

        public void Generate()
        {
            List<Spell> spells = new List<Spell>();

            using (StreamWriter w = new StreamWriter($@"C:\lol\lol.csv"))
            {
                foreach (TalentList tal in (TalentList[])Enum.GetValues(typeof(TalentList)))
                {
                    Spell spell = _getSpell.FromEnum(tal);
                    string line = $"{spell.Name},{spell.CharacterClass},{spell.SpellType},{spell.EnergyCost},{spell.MinionsCost},{spell.Initiative},{spell.NbTargets},{spell.CriticalChance}";
                    w.WriteLine(line);
                    w.Flush();
                }
            }
        }
    }
}
