using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI.CharAction.Tgt
{
    public class RandomSpellChooser : ISpellChooser
    {
        public Spell ChooseSpell(Character charToPlay)
        {
            var possibleSpell = charToPlay.CharacterTalentStats.UnlockedSpells
                .Where(x => x.EnergyCost <= charToPlay.Energy && (!x.MinionsCost.HasValue || x.MinionsCost.Value <= charToPlay.ExtraPoint)).ToList();
            Random rnd = new Random();
            int source = rnd.Next(possibleSpell.Count);
            Spell spell = possibleSpell[source];

            return spell;
        }
    }
}
