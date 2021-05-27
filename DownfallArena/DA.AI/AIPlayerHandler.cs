using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Services;
using DA.Game.Events;

namespace DA.AI
{
    public class AIPlayerHandler : BasePlayerHandler
    {
        private List<Character> GetMyAliveCharacters()
        {
            List<Character> myAliveCharacters = new List<Character>();
            if (this.Indicator == TeamIndicator.One)
            {
                myAliveCharacters = (List<Character>)Battle.TeamOne.AliveCharacters;
            }
            else
            {
                myAliveCharacters = (List<Character>)Battle.TeamTwo.AliveCharacters;
            }

            return myAliveCharacters;
        }

        private List<Character> GetMyEnemies()
        {
            List<Character> myEnemies = new List<Character>();
            if (this.Indicator == TeamIndicator.One)
            {
                myEnemies = (List<Character>)Battle.TeamTwo.AliveCharacters;
            }
            else
            {
                myEnemies = (List<Character>)Battle.TeamOne.AliveCharacters;
            }

            return myEnemies;
        }

        public AIPlayerHandler(IBattleEngine battleService) : base(battleService)
        {

        }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            List<Character> myAliveCharacters = GetMyAliveCharacters();
            var characterToPlay = myAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);

            var a = GetMyEnemies();
            if (characterToPlay != null)
            {
                BattleEngine.PlayAndResolveCharacterAction(Battle, new CharacterActionChoice()
                {
                    CharacterId = e.CharacterId,
                    Spell = characterToPlay.CharacterTalentStats.UnlockedSpells.Where(x => x.EnergyCost <= characterToPlay.Energy).ToList()[0],
                    Targets = new List<Guid>() { a[0].Id}
                });
            }

        }

        public override void SpellUnlock(object sender, EventArgs e)
        {
            List<Character> myAliveCharacters = GetMyAliveCharacters();
            List<SpellUnlockChoice> choices = new List<SpellUnlockChoice>();

            foreach (var c in myAliveCharacters)
            {
                choices.Add(new SpellUnlockChoice()
                {
                    CharacterId = c.Id,
                    Spell = c.TalentTreeStructure.Root.GetNextChildrenToUnlock()[0].Spell
                });
            }
            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            List<Character> myAliveCharacters = GetMyAliveCharacters();

            List<SpeedChoice> choices = new List<SpeedChoice>();
            var rnd = new Random();

            foreach (var c in myAliveCharacters)
            {
                choices.Add(new SpeedChoice()
                {
                    CharacterId = c.Id,
                    Speed = rnd.NextDouble() < 0.5 ? Speed.Quick : Speed.Standard
                });
            }

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
