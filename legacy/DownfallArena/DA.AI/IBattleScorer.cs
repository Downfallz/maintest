using DA.Game.Domain.Models;

namespace DA.AI;

public interface IBattleScorer
{
    double GetBattleScore(Battle battle);
}