using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Services.TalentsManagement
{
    public interface ICharacterDevelopmentService
    {
        Character InitializeNewCharacter();
        void UnlockSpell(Character character, Spell talent);
    }
}