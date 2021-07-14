using DA.Game.Domain.Models.GameFlowEngine;
using System.Linq;

namespace DA.AI
{
    public class BattleScorer
    {


        public int GetBattleScore(Battle battle)
        {
            int teamOneAlivePoint = battle.TeamOne.AliveCharacters.Count * 10;
            int teamOneTotalHp = battle.TeamOne.AliveCharacters.Sum(x => x.Health);
            int totalTeamOneScore = teamOneAlivePoint + teamOneTotalHp;

            int teamTwoAlivePoint = battle.TeamTwo.AliveCharacters.Count * 10;
            int teamTwoTotalHp = battle.TeamTwo.AliveCharacters.Sum(x => x.Health);
            int totalTeamTwoScore = teamTwoAlivePoint + teamTwoTotalHp;

            return totalTeamTwoScore - totalTeamOneScore;

        }
    }
}
