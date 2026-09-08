using System.Collections.Generic;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;

namespace DA.Core.Game.AI.Spl
{
    public interface ISpellChooser
    {
        List<SpellUnlockChoice> GetSpellUnlockChoices(List<Character> aliveCharacters);
    }
}