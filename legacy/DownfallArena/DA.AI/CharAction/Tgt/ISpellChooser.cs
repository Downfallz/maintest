using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI.CharAction.Tgt;

public interface ISpellChooser
{
    public Spell ChooseSpell(Character charToPlay);
}