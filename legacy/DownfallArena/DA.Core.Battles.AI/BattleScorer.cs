using System.Linq;
using DA.Core.Domain.Battles;

namespace DA.Core.Game.AI
{
    public class BattleScorer
    {
   

        public int GetBattleScore(Battle battle)
        {
            var teamOneAlivePoint = battle.TeamOne.AliveCharacters.Count * 10;
            var teamOneTotalHp = battle.TeamOne.AliveCharacters.Sum(x => x.Health);
            var totalTeamOneScore = teamOneAlivePoint + teamOneTotalHp;

            var teamTwoAlivePoint = battle.TeamTwo.AliveCharacters.Count * 10;
            var teamTwoTotalHp = battle.TeamTwo.AliveCharacters.Sum(x => x.Health);
            var totalTeamTwoScore = teamTwoAlivePoint + teamTwoTotalHp;

            return totalTeamTwoScore - totalTeamOneScore;

        }
    }
}
