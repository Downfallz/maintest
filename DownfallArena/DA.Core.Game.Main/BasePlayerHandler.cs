using System;
using System.Collections.Generic;
using DA.Core.Game.Main.Events;

namespace DA.Core.Game.Main
{
    public abstract class BasePlayerHandler
    {
        protected IBattleEngine BattleEngine {get;}
        public BasePlayerHandler(IBattleEngine battleService)
        {
            BattleEngine = battleService;
        }
        protected Battle Battle { get; private set; }W
        protected TeamIndicator Indicator { get; private set; }
        public void Setup(Battle battle, TeamIndicator indicator)
        {
            Battle = battle;
            Indicator = indicator;
        }

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
                    myEnemies = (List<Character>) Battle.TeamTwo.AliveCharacters;
                }
                else
                {
                    myEnemies = (List<Character>) Battle.TeamOne.AliveCharacters;
                }

                return myEnemies;
            }
        }
    }
}
