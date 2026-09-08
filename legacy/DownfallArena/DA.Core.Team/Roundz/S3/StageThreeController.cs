using System.Collections.Generic;

namespace DA.Core.Team.Roundz.S3
{
    public class StageThreeController
    {
        private Battle _battle;
        public StageThreeController(Battle battle)
        {
            _battle = battle;
        }

        public StageThree MakeChoiceAndApply(List<IndexTalent> indexTalents)
        {
            var choice = new StageThreeChoice();
            choice.TalentChoices = indexTalents;

            // Algorithm pour l'ordre
            var result = new StageOneResult();
            var stageThree = new StageThree();
            stageThree.StageThreeChoice = choice;

            return stageThree;
        }
    }
}
