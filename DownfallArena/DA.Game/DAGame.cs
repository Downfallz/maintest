using DA.Game.Domain.Models.GameFlowEngine.Enum;

namespace DA.Game
{
    public class DAGame : IDAGame
    {
        private readonly BattleEngine _battleEngine;

        public DAGame(BattleEngine battleService)
        {
            _battleEngine = battleService;
        }

        public void Start(BasePlayerHandler one, BasePlayerHandler two)
        {
            var battle = _battleEngine.InitializeNewBattle();
            one.Setup(battle, TeamIndicator.One);
            two.Setup(battle, TeamIndicator.Two);

            SetupPlayerHandler(one);
            SetupPlayerHandler(two);

            _battleEngine.StartBattle(battle);
        }

        private void SetupPlayerHandler(BasePlayerHandler ph)
        {
            _battleEngine.NewRoundInitialized += ph.SpellUnlock;
            _battleEngine.AllSpellUnlocked += ph.SpeedChoose;
            _battleEngine.CharacterTurnInitialized += ph.EvaluateCharacterToPlay;
        }
    }
}
