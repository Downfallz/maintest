using System;
using System.Collections.Generic;
using System.Linq;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;

namespace DA.AI.MonteCarlo
{
    public class State
    {
        public State() { }
        public State(State state)
        {
            Board = state.Board.Clone();
            PlayerNo = state.PlayerNo;
            VisitCount = state.VisitCount;
            Score = state.Score;

        }


        public Battle Board { get; set; }
        public int PlayerNo { get; set; }
        public int VisitCount { get; set; }
        public int Score { get; set; }

        public int GetOpponent()
        {
            return 3 - PlayerNo;
        }
        // copy constructor, getters, and setters

        public List<State> GetAllPossibleStates(IBattleEngine be)
        {
            List<State> possibleStates = new List<State>();

            List<CharacterActionChoice> sd = GetAvailablePosition(be);

            foreach (var ap in sd)
            {
                State newState = new State();
                newState.Board = Board.Clone();
                if (newState.Board.CurrentRound == null || !newState.Board.CurrentRound.CurrentCharacterIndex.HasValue)
                {
                    Setup(be, newState.Board);
                }
                newState.PlayerNo = newState.Board.CurrentRound.OrderedCharacters[newState.Board.CurrentRound.CurrentCharacterIndex.Value].TeamNumber;
                be.PlayAndResolveCharacterAction(newState.Board, ap);
                possibleStates.Add(newState);
            }

            return possibleStates;
        }

        private List<CharacterActionChoice> GetAvailablePosition(IBattleEngine be)
        {
            List<CharacterActionChoice> sd = new List<CharacterActionChoice>();
            if (Board.CurrentRound == null || Board.CurrentRound.CurrentCharacterIndex == null)
            {
                Setup(be, Board);
            }

            var charToPlay = Board.CurrentRound.OrderedCharacters[Board.CurrentRound.CurrentCharacterIndex.Value];

            foreach (var s in charToPlay.CharacterTalentStats.UnlockedSpells.Where(x => x.EnergyCost <= charToPlay.Energy))
            {
                Team teamTarget = null;
                if (s.SpellType == SpellType.Defensive)
                {
                    if (charToPlay.TeamNumber == 1)
                    {
                        teamTarget = Board.TeamOne;
                    }
                    else
                    {
                        teamTarget = Board.TeamTwo;
                    }
                }
                else if (s.SpellType == SpellType.Offensive)
                {
                    if (charToPlay.TeamNumber == 1)
                    {
                        teamTarget = Board.TeamTwo;
                    }
                    else
                    {
                        teamTarget = Board.TeamOne;
                    }
                }

                if (s.NbTargets == 1)
                {
                    foreach (var target in teamTarget.AliveCharacters)
                    {
                        sd.Add(new CharacterActionChoice()
                        {
                            CharacterId = charToPlay.Id,
                            Spell = s,
                            Targets = new List<Guid>() { target.Id }
                        });
                    }
                }
                else
                {
                    sd.Add(new CharacterActionChoice()
                    {
                        CharacterId = charToPlay.Id,
                        Spell = s,
                        Targets = teamTarget.AliveCharacters.Select(x => x.Id).ToList()
                    });
                }
            }

            return sd;
        }

        private void Setup(IBattleEngine be, Battle battle)
        {
            var spellUnlock = new SpellChooser();
            var t1Choice = spellUnlock.GetSpellUnlockChoices(battle.TeamOne.AliveCharacters.ToList());
            var t2Choice = spellUnlock.GetSpellUnlockChoices(battle.TeamTwo.AliveCharacters.ToList());

            var speedChooser = new SpeedChooser();
            var t1SpeedChoice = speedChooser.GetSpeedChoices(battle,
                battle.TeamOne.AliveCharacters.ToList(),
                battle.TeamTwo.AliveCharacters.ToList());
            var t2SpeedChoice = speedChooser.GetSpeedChoices(battle,
                battle.TeamTwo.AliveCharacters.ToList(),
                battle.TeamOne.AliveCharacters.ToList());

            be.ChooseSpellToUnlock(battle, TeamIndicator.One, t1Choice);
            be.ChooseSpellToUnlock(battle, TeamIndicator.Two, t2Choice);
            be.ChooseSpeed(battle, TeamIndicator.One, t1SpeedChoice);
            be.ChooseSpeed(battle, TeamIndicator.Two, t2SpeedChoice);
        }

        internal void RandomPlay(IBattleEngine be)
        {
            List<CharacterActionChoice> availablePositions = GetAvailablePosition(be);
            int totalPossibilities = availablePositions.Count;
            var rnd = new Random();
            int selectRandom = rnd.Next(0, totalPossibilities);
            be.PlayAndResolveCharacterAction(Board, availablePositions[selectRandom]);
        }

        internal void IncrementVisit()
        {
            VisitCount++;
        }

        internal void AddScore(int winScore)
        {
            if (Score != int.MinValue)
                Score += winScore;
        }

        internal void TogglePlayer()
        {
            PlayerNo = 3 - PlayerNo;
        }
    }
}
