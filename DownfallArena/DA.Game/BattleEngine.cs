using System;
using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Services;
using DA.Game.Domain.Services.GameFlowEngine;
using DA.Game.Events;

namespace DA.Game
{
    public class BattleEngine : IBattleEngine
    {
        private readonly ITeamService _teamService;
        private readonly IRoundService _roundService;

        public event EventHandler NewRoundInitialized;
        public event EventHandler AllSpellUnlocked;
        public event EventHandler AllSpeedChosen;
        public event EventHandler<CharacterTurnInitializedEventArgs> CharacterTurnInitialized;

        protected virtual void OnNewRoundInitialized(EventArgs e)
        {
            EventHandler handler = NewRoundInitialized;
            handler?.Invoke(this, e);
        }
        protected virtual void OnAllSpellUnlocked(EventArgs e)
        {
            EventHandler handler = AllSpellUnlocked;
            handler?.Invoke(this, e);
        }
        protected virtual void OnAllSpeedChosen(EventArgs e)
        {
            EventHandler handler = AllSpeedChosen;
            handler?.Invoke(this, e);
        }
        protected virtual void OnCharacterTurnInitialized(CharacterTurnInitializedEventArgs e)
        {
            EventHandler<CharacterTurnInitializedEventArgs> handler = CharacterTurnInitialized;
            handler?.Invoke(this, e);
        }

        public BattleEngine(ITeamService teamService, IRoundService roundService)
        {
            _teamService = teamService;
            _roundService = roundService;
        }

        public Battle InitializeNewBattle()
        {
            var battle = new Battle();

            battle.BattleStatus = BattleStatus.Created;

            if (battle.BattleStatus != BattleStatus.Created)
                throw new Exception("Invalid battle status to be adding players.");


            battle.TeamOne = _teamService.InitializeNewTeam();
            foreach(var c in battle.TeamOne.Characters)
            {
                c.TeamNumber = 1;
            }
            battle.TeamTwo = _teamService.InitializeNewTeam();
            foreach (var c in battle.TeamTwo.Characters)
            {
                c.TeamNumber = 2;
            }

            return battle;
        }

        public void StartBattle(Battle battle)
        {
            if (battle.BattleStatus == BattleStatus.Created &&
                battle.TeamOne != null &&
                battle.TeamTwo != null &&
                battle.FinishedRoundsHistory.Count == 0 &&
                battle.CurrentRound == null)
            {
                battle.BattleStatus = BattleStatus.Started;

                _roundService.InitializeNewRound(battle);

                OnNewRoundInitialized(EventArgs.Empty);
            }
            else
            {
                throw new Exception("Invalid game state to be starting battle.");
            }

        }

        public void ChooseSpellToUnlock(Battle battle, TeamIndicator ti, List<SpellUnlockChoice> choices)
        {
            if (choices.Count > 2)
                throw new Exception("Can't unlock more than 2 players at a time.");
            if (_roundService.ChooseTeamSpellToUnlock(battle, ti, choices))
            {
                _roundService.ApplySpellsToUnlock(battle);
                OnAllSpellUnlocked(EventArgs.Empty);
            }
        }

        public void ChooseSpeed(Battle battle, TeamIndicator ti, List<SpeedChoice> choices)
        {
            if (_roundService.ChooseTeamSpeed(battle, ti, choices))
            {
                _roundService.ResolveCharacterOrder(battle);
                var c = _roundService.GetCurrentCharacterIdActionTurn(battle.CurrentRound);
                OnCharacterTurnInitialized(new CharacterTurnInitializedEventArgs() { CharacterId = c.Value });
            }
        }

        public void PlayAndResolveCharacterAction(Battle battle, CharacterActionChoice characterActionChoice)
        {
            _roundService.PlayAndResolveCharacterAction(battle.CurrentRound, characterActionChoice);
            var c = _roundService.GetCurrentCharacterIdActionTurn(battle.CurrentRound);

            var characterContinueToPlay = false;

            if (c == null)
            {
                if (!battle.IsDone)
                {
                    _roundService.InitializeNewRound(battle);
                    OnNewRoundInitialized(EventArgs.Empty);
                    //characterContinueToPlay = true;
                }
                else
                {
                    battle.BattleStatus = BattleStatus.Finished;
                }
            }
            else
            {
                characterContinueToPlay = true;
            }

            if (characterContinueToPlay)
            {
                OnCharacterTurnInitialized(new CharacterTurnInitializedEventArgs() { CharacterId = c.Value });
            }
        }
    }
}
