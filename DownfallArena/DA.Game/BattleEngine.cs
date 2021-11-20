using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.Enum;

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
            Battle battle = new Battle
            {
                BattleStatus = BattleStatus.Created
            };

            if (battle.BattleStatus != BattleStatus.Created)
                throw new Exception("Invalid battle status to be adding players.");


            battle.TeamOne = _teamService.InitializeNewTeam();
            foreach (Character c in battle.TeamOne.Characters)
            {
                c.TeamNumber = 1;
            }
            battle.TeamTwo = _teamService.InitializeNewTeam();
            foreach (Character c in battle.TeamTwo.Characters)
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
                Guid? c = _roundService.GetCurrentCharacterIdActionTurn(battle.CurrentRound);
                OnCharacterTurnInitialized(new CharacterTurnInitializedEventArgs() { CharacterId = c.Value });
            }
        }

        public void PlayAndResolveCharacterAction(Battle battle, CharacterActionChoice characterActionChoice)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));

            if (battle.BattleStatus != BattleStatus.Started)
                throw new System.Exception("Can't play a character action if battle is not in status started");

            _roundService.PlayAndResolveCharacterAction(battle.CurrentRound, characterActionChoice);
            _roundService.AssignNextCharacter(battle.CurrentRound);
            Guid? c = _roundService.GetCurrentCharacterIdActionTurn(battle.CurrentRound);

            bool characterContinueToPlay = false;

            // Si c == null , fin du round.
            if (c == null)
            {
                battle.FinishedRoundsHistory.Add(battle.CurrentRound);

                if (!battle.OneTeamIsDead && battle.FinishedRoundsHistory.Count <= 15)
                {
                    _roundService.InitializeNewRound(battle);
                    OnNewRoundInitialized(EventArgs.Empty);
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
