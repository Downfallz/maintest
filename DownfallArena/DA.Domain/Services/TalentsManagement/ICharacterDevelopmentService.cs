using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Domain.Services.GameFlowEngine.TalentsManagement
{
    public interface ICharacterDevelopmentService
    {
        Character InitializeNewCharacter();
        void UnlockSpell(Character character, Spell talent);
    }
}