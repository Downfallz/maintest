using System.Collections.Generic;

namespace DA.Core.Team.Roundz.S1
{
    public class StageOneController
    {
        private Battle _battle;
        public StageOneController(Battle battle)
        {
            _battle = battle;
        }

        public StageOne MakeChoiceAndResolveResult(List<IndexInitiative> indexInitiatives)
        {
            var choice = new StageOneChoice();
            choice.InitiativesChoice = indexInitiatives;

            // Algorithm pour l'ordre
            var result = new StageOneResult();
            var stageOne = new StageOne();
            stageOne.StageOneChoice = choice;
            stageOne.StageOneResult = result;
            return stageOne;
        }
    }
}
