using System;
using System.Collections.Generic;
using System.Linq;
using DA.Game.Domain.Models.Enum;

namespace DA.Game.Domain.Models
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

        public bool OneTeamIsDead => TeamOne.IsDead || TeamTwo.IsDead;

        public int Winner
        {
            get
            {
                int winner = OneTeamIsDead ? (TeamOne.IsDead ? 2 : 1) : -1;
                if (winner == -1)
                {
                    if (FinishedRoundsHistory.Count >= 15)
                    {
                        if (TeamOne.AliveCharacters.Sum(x => x.Health) > TeamTwo.AliveCharacters.Sum(x => x.Health))
                        {
                            return 1;
                        }
                        else
                        {
                            return 2;
                        }
                    }
                }
                return winner;
                
            }
        }

    }
}
