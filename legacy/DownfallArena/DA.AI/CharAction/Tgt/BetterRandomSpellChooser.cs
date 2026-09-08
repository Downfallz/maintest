using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI.CharAction.Tgt
{
    public class BetterRandomSpellChooser : ISpellChooser
    {
        public Spell ChooseSpell(Character charToPlay)
        {
            Random rnd = new Random();
            Spell spell;
            var possibleSpell = charToPlay.CharacterTalentStats.UnlockedSpells
                .Where(x => x.EnergyCost <= charToPlay.Energy && (!x.MinionsCost.HasValue || x.MinionsCost.Value <= charToPlay.ExtraPoint)).ToList();

            if (possibleSpell.Count == 1) // means wait only
            {
                spell = possibleSpell[0];
            }
            else if (possibleSpell.Count < charToPlay.CharacterTalentStats.UnlockedSpells.Count) // means more powerful spell we cannot play yet
            {
                // Only spell that cost 1 or 0

                do
                {
                    int index = rnd.Next(possibleSpell.Count);
                    spell = possibleSpell[index];
                } while (spell.EnergyCost > 1);
            }
            else
            {
                // Only spell that cost 2 or more, level 3 ideally
                List<Spell> moreStrong = new List<Spell>();
                if (possibleSpell.Any(x => x.Level == 3))
                    moreStrong = possibleSpell.Where(x => x.Level == 3).ToList();
                else
                {
                    moreStrong = possibleSpell.Where(x => x.EnergyCost >= 2).ToList();
                }

                int index = rnd.Next(moreStrong.Count);
                spell = moreStrong[index];
            }

            return spell;
        }
    }
}
