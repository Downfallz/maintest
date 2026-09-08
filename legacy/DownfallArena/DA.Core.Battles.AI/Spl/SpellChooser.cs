using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;

namespace DA.Core.Game.AI.Spl
{
    public class SpellChooser : ISpellChooser
    {
        public List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters)
        {
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();
            var rnd = new Random();

            int count = 0;
            foreach (var c in aliveCharacters)
            {
                if (count == 2)
                    break;
                var result = rnd.Next(0, 5);
                var possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                if (possibleList.Any())
                {
                    List<KeyValuePair<int, List<Domain.Base.Talents.Talents.TalentNode>>> level = new List<KeyValuePair<int, List<Domain.Base.Talents.Talents.TalentNode>>>();
                    Spell spellToUnlock;
                    if (result > 1)
                    {
                        level = possibleList.GroupBy(o => o.Spell.Level)
                                    .ToDictionary(g => g.Key, g => g.ToList()).OrderByDescending(x => x.Key).ToList();

                    }
                    else
                    {
                        level = possibleList.GroupBy(o => o.Spell.Level)
                                    .ToDictionary(g => g.Key, g => g.ToList()).OrderBy(x => x.Key).ToList();
                    }

                    var spellCount = level.First().Value.Count;
                    var target = rnd.Next(0, spellCount);
                    spellToUnlock = level.First().Value[target].Spell;

                    choices.Add(new SpellUnlockChoice()
                    {
                        CharacterId = c.Id,
                        Spell = spellToUnlock
                    });
                }
                
                count++;
            }

            return choices;
        }
    }
}
