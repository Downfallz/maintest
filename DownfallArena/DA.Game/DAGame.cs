using DA.Game.Domain.Models;
using DA.Game.Domain.Models.Enum;

namespace DA.Game
{
    public class DAGame : IDAGame
    {
        private readonly BattleController _battleController;
        public Battle Battle { get; private set; }
        public DAGame(BattleController battleController)
        {
            _battleController = battleController;
        }

        public void Start(BasePlayerHandler one, BasePlayerHandler two)
        {
            Battle = _battleController.InitializeNewBattle();
            one.Setup(Battle, TeamIndicator.One);
            two.Setup(Battle, TeamIndicator.Two);

            SetupPlayerHandler(one);
            SetupPlayerHandler(two);

            _battleController.StartBattle(Battle);
        }

        private void SetupPlayerHandler(BasePlayerHandler ph)
        {
            _battleController.NewRoundInitialized += ph.SpellUnlock;
            _battleController.AllSpellUnlocked += ph.SpeedChoose;
            _battleController.CharacterTurnInitialized += ph.EvaluateCharacterToPlay;
            _battleController.CharacterPlayed += ph.CharacterPlayed;
        }
    }
}
