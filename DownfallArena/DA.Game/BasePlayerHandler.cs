using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using DA.Game.Domain;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.Enum;

namespace DA.Game
{
    public abstract class BasePlayerHandler
    {
        protected IBattleController BattleEngine { get; }
        public BasePlayerHandler(IBattleController battleService)
        {
            BattleEngine = battleService;
        }
        protected Battle Battle { get; private set; }
        protected TeamIndicator Indicator { get; private set; }
        public void Setup(Battle battle, TeamIndicator indicator)
        {
            Battle = battle;
            Indicator = indicator;
        }
        public abstract void CharacterPlayed(object sender, CharacterPlayedEventArgs e);
        public abstract void SpellUnlock(object sender, EventArgs e);
        public abstract void SpeedChoose(object sender, EventArgs e);
        public abstract void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e);

        protected List<Character> MyAliveCharacters
        {
            get
            {
                List<Character> myAliveCharacters;
                if (Indicator == TeamIndicator.One)
                {
                    myAliveCharacters = (List<Character>)Battle.TeamOne.AliveCharacters;
                }
                else
                {
                    myAliveCharacters = (List<Character>)Battle.TeamTwo.AliveCharacters;
                }

                return myAliveCharacters;
            }
        }

        protected List<Character> MyEnemies
        {
            get
            {
                List<Character> myEnemies;
                if (Indicator == TeamIndicator.One)
                {
                    myEnemies = (List<Character>)Battle.TeamTwo.AliveCharacters;
                }
                else
                {
                    myEnemies = (List<Character>)Battle.TeamOne.AliveCharacters;
                }

                return myEnemies;
            }
        }
    }
}
