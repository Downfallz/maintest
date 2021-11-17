using DA.Game.Domain.Models;
using DA.Game.Domain.Models.Enum;

namespace DA.Game
{
    public class DAGame : IDAGame
    {
        private readonly BattleEngine _battleEngine;
        public Battle Battle { get; private set; }
        public DAGame(BattleEngine battleService)
        {
            _battleEngine = battleService;
        }

        public void Start(BasePlayerHandler one, BasePlayerHandler two)
        {
            Battle = _battleEngine.InitializeNewBattle();
            one.Setup(Battle, TeamIndicator.One);
            two.Setup(Battle, TeamIndicator.Two);

            SetupPlayerHandler(one);
            SetupPlayerHandler(two);

            _battleEngine.StartBattle(Battle);
        }

        private void SetupPlayerHandler(BasePlayerHandler ph)
        {
            _battleEngine.NewRoundInitialized += ph.SpellUnlock;
            _battleEngine.AllSpellUnlocked += ph.SpeedChoose;
            _battleEngine.CharacterTurnInitialized += ph.EvaluateCharacterToPlay;
        }
    }
}
