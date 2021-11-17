using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using DA.AI.CharAction;
using DA.AI.CharAction.Tgt;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI
{
    public class RandomAIPlayerHandler : BaseAIPlayerHandler
    {
        public RandomAIPlayerHandler(IBattleEngine battleService) : base(battleService, new RandomSpeedChooser(), 
            new RandomSpellUnlockChooser(), 
            new BasicCharacterActionChooser(new RandomSpellChooser(), new RandomTargetChooser()))
        {
        }
    }
}
