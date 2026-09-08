using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Teams.Abstractions
{
    public interface ICharacterDevelopmentService
    {
        Character InitializeNewCharacter();
        void UnlockSpell(Character character, Spell talent);
    }
}