using System;
using System.Collections.Generic;
using System.Text;
using DA.Core.Abilities.Talents;
using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Team
{
    public class CharacterController
    {
        private readonly ITalentTreeService _talentTreeController;

        public CharacterController(ITalentTreeService talentTreeController)
        {
            _talentTreeController = talentTreeController;
        }
        public Character InitializeNewCharacter()
        {
            return new Character();
        }

        public void UnlockTalent(Character character, Talent talent)
        {
            var result =_talentTreeController.UnlockTalent(character.TalentTree, talent);
            if (result)
            { }
                var asd = "apply bonus";
        }

    }
}
