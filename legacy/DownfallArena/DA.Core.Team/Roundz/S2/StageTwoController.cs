namespace DA.Core.Team.Roundz.S2
{
    public class StageTwoController
    {
        private Battle _battle;
        public StageTwoController(Battle battle)
        {
            _battle = battle;
        }

        public StageTwo MakeChoiceAndResolveResult(CharTurnChoice choice)
        {
            // Validate if good char's turn to play
            // resolve spell yadayada
            // apply stuff and update status
        }
    }
}
