using DA.Game.Domain.Models.GameFlowEngine.Enum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DA.Game.Domain.Models.GameFlowEngine
{
    [Serializable]
    public class Battle
    {
        public Battle()
        {
            FinishedRoundsHistory = new List<Round>();
        }

        public Team TeamOne { get; set; }

        public Team TeamTwo { get; set; }

        public BattleStatus BattleStatus { get; set; }

        public IList<Round> FinishedRoundsHistory { get; set; }

        public Round CurrentRound { get; set; }

        public List<Character> AllAliveCharacters => TeamOne.AliveCharacters.Concat(TeamTwo.AliveCharacters).ToList();

        public List<Character> AllCharacter => TeamOne.Characters.Concat(TeamTwo.Characters).ToList();

        public bool IsDone => TeamOne.IsDead || TeamTwo.IsDead;

        public int Winner => IsDone ? (TeamOne.IsDead ? 2 : 1) : -1;
    }
}
