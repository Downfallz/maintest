using DA.AI.MonteCarlo;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.Domain.Services;
using DA.Game.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using DA.AI.CharAction.Tgt;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.AI
{
    public class SuperAIPlayerHandler : BasePlayerHandler
    {
        readonly IBattleEngine _simulator;
        private readonly ISpeedChooser _sc;
        private readonly ISpellUnlockChooser _spellChooser;
        private readonly ITargetChooser _targetChooser;

        public SuperAIPlayerHandler(IBattleEngine battleService,
            IBattleEngine simulator, ISpeedChooser sc, ISpellUnlockChooser spellChooser,
            ITargetChooser targetChooser) : base(battleService)
        {
            _simulator = simulator;
            _sc = sc;
            _spellChooser = spellChooser;
            _targetChooser = targetChooser;
        }

        public override void EvaluateCharacterToPlay(object sender, CharacterTurnInitializedEventArgs e)
        {
            Character characterToPlay = MyAliveCharacters.SingleOrDefault(x => x.Id == e.CharacterId);

            if (characterToPlay != null)
            {
                MonteCarloTreeSearch monte = new MonteCarloTreeSearch(_simulator);
                monte.FindNextMove(Battle, 2);
                BattleScorer battleScorer = new BattleScorer();
                Spell bestSpell = null;
                List<Guid> bestTargets = null;
                int bestScore = battleScorer.GetBattleScore(Battle);

                foreach (Spell spell in characterToPlay.CharacterTalentStats.UnlockedSpells.Where(x => x.EnergyCost <= characterToPlay.Energy).ToList())
                {
                    List<Guid> targets = _targetChooser.ChooseTargetForSpell(spell, MyAliveCharacters, MyEnemies);

                    Battle battleClone = Battle.Clone();

                    _simulator.PlayAndResolveCharacterAction(battleClone, new CharacterActionChoice()
                    {
                        CharacterId = e.CharacterId,
                        Spell = spell,
                        Targets = targets
                    });


                    int score = battleScorer.GetBattleScore(battleClone);
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
            List<SpellUnlockChoice> choices = _spellChooser.GetSpellUnlockChoices(MyAliveCharacters);
            BattleEngine.ChooseSpellToUnlock(Battle, Indicator, choices);
        }

        public override void SpeedChoose(object sender, EventArgs e)
        {
            List<SpeedChoice> choices = _sc.GetSpeedChoices(Battle, MyAliveCharacters, MyEnemies);

            BattleEngine.ChooseSpeed(Battle, Indicator, choices);
        }
    }
}
