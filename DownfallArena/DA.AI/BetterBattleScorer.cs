using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using DA.Game.Domain.Models;

namespace DA.AI
{
    public record TeamScoreStats()
    {
        public int AliveCount { get; set; }
        public int TotalHp { get; set; }
        public int TotalEnergy { get; set; }
        public int TotalMinions { get; set; }
        public int TotalInitiative { get; set; }
        public int TotalStunned { get; set; }
        public double TotalCritBonus { get; set; }
        public int TotalBonusDef { get; set; }
        public int TotalBonusInitiative { get; set; }
        public int TotalBonusRetaliate { get; set; }
    }

    public class BetterBattleScorer : IBattleScorer
    {
        private const int ALIVE_WEIGHT = 10;
        private const double ENERGY_WEIGHT = 0.5;
        private const double INITIATIVE_WEIGHT = 0.3;
        private const int STUNNED_WEIGHT = 4;
        private const double CRIT_WEIGHT = 30;

        public double GetBattleScore(Battle battle)
        {
            var teamOneStats = TeamScoreStats(battle.TeamOne.AliveCharacters);
            var teamTwoStats = TeamScoreStats(battle.TeamTwo.AliveCharacters);
            double teamOneScore = CalculateScore(teamOneStats);
            double teamTwoScore = CalculateScore(teamTwoStats);
            return teamTwoScore - teamOneScore;
        }

        private double CalculateScore(TeamScoreStats teamOneStats)
        {
            var alivePoint = teamOneStats.AliveCount * ALIVE_WEIGHT;
            int totalHpPoint = teamOneStats.TotalHp;

            double totalEnergyPoint = teamOneStats.TotalEnergy * ENERGY_WEIGHT;
            double totalMinionsPoint = teamOneStats.TotalMinions > 5 ? 5 : teamOneStats.TotalMinions;
            double totalInitiativePoint = teamOneStats.TotalInitiative * INITIATIVE_WEIGHT;
            double totalStunnedPoint = teamOneStats.TotalStunned * STUNNED_WEIGHT;
            double totalCritBonusPoint = teamOneStats.TotalCritBonus * CRIT_WEIGHT;
            double totalBonusDefPoint = teamOneStats.TotalBonusDef;
            double totalBonusInitiativePoint = teamOneStats.TotalBonusInitiative;
            double totalBonusRetaliatePoint = teamOneStats.TotalBonusRetaliate;

            return alivePoint + totalHpPoint + totalEnergyPoint + totalMinionsPoint + totalInitiativePoint
                + totalStunnedPoint + totalCritBonusPoint + totalBonusDefPoint + totalBonusInitiativePoint + totalBonusRetaliatePoint;
        }

        private static TeamScoreStats TeamScoreStats(IList<Character> characters)
        {
            var teamOneStats = new TeamScoreStats();
            teamOneStats.AliveCount = characters.Count;
            teamOneStats.TotalHp = characters.Sum(x => x.Health);
            teamOneStats.TotalEnergy = characters.Sum(x => x.Energy);
            teamOneStats.TotalMinions = characters.Sum(x => x.ExtraPoint);
            teamOneStats.TotalInitiative = characters.Sum(x => x.Initiative);
            teamOneStats.TotalStunned = characters.Count(x => x.IsStunned);
            teamOneStats.TotalCritBonus = characters.Sum(x => x.BonusCritical);
            teamOneStats.TotalBonusDef = characters.Sum(x => x.BonusDefense);
            teamOneStats.TotalBonusInitiative = characters.Sum(x => x.BonusInitiative);
            teamOneStats.TotalBonusRetaliate = characters.Sum(x => x.BonusRetaliate);
            return teamOneStats;
        }
    }
}
