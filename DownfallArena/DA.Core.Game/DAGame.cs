using DA.Core.Domain.Base.Teams.Enum;
using DA.Core.Game.Main;

namespace DA.Core.Game
{
    public class DAGame : IDAGame
    {
        private readonly IBattleEngine _battleEngine;

        public DAGame(IBattleEngine battleService)
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
