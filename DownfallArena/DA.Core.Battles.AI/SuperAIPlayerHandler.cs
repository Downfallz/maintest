using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Base.Teams.Enum;
using DA.Core.Domain.Battles;
using DA.Core.Domain.Battles.Enum;
using DA.Core.Game.AI.MonteCarlo;
using DA.Core.Game.AI.Spd;
using DA.Core.Game.AI.Spl;
using DA.Core.Game.AI.Tgt;
using DA.Core.Game.Main;
using DA.Core.Game.Main.Events;

namespace DA.Core.Game.AI
{
    public class SuperAIPlayerHandler : BasePlayerHandler
    {
        IBattleEngine _simulator;
        private readonly ISpeedChooser _sc;
        private readonly ISpellChooser _spellChooser;
        private readonly ITargetChooser _targetChooser;

        public SuperAIPlayerHandler(IBattleEngine battleService, 
            IBattleEngine simulator, ISpeedChooser sc, ISpellChooser spellChooser,
            ITargetChooser targetChooser) : base(battleService)
        {
            _simulator = simulator;
            _sc = sc;
            _spellChooser = spellChooser;
            _targetChooser = targetChooser;
        }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            var characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);

            if (characterToPlay != null)
            {
                var monte = new MonteCarloTreeSearch(_simulator);
                monte.FindNextMove(Battle, 2);
                var battleScorer = new BattleScorer();
                Spell bestSpell = null;
                List<Guid> bestTargets = null;
                int bestScore = battleScorer.GetBattleScore(Battle);

                foreach (var spell in characterToPlay.CastableSpells)
                {
                    List<Guid> targets = _targetChooser.ChooseTargetForSpell(spell, MyAliveCharacters, MyEnemies);

                    var battleClone = Battle.Clone();

                    _simulator.PlayAndResolveCharacterAction(battleClone, new CharacterActionChoice()
                    {
                        CharacterId = e.CharacterId,
                        Spell = spell,
                        Targets = targets
                    });


                    var score = battleScorer.GetBattleScore(battleClone);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTargets = targets;
                        bestSpell = spell;
                    }
                }

                BattleEngine.PlayAndResolveCharacterAction(Battle, new CharacterActionChoice()
                {
                    CharacterId = e.CharacterId,
                    Spell = bestSpell,
                    Targets = bestTargets
                });
            }
        }

        

        public override void SpellUnlock(object sender, EventArgs e)
        {
            var choices = _spellChooser.GetSpellUnlockChoices(MyAliveCharacters);
            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            var choices = _sc.GetSpeedChoices(Battle, MyAliveCharacters, MyEnemies);

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
