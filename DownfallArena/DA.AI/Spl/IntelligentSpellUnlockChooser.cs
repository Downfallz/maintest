using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using DA.AI.CharAction;
using DA.AI.Spd;
using DA.Game;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.Enum;
using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;
using Force.DeepCloner;

namespace DA.AI.Spl
{

    public record BestPickSpellChoose()
    {
        public List<SpellUnlockChoice> Choices { get; set; }
        public double BestScore { get; set; }
    }
    public class IntelligentSpellUnlockChooser : ISpellUnlockChooser
    {
        private readonly IBattleEngine _sim;
        private readonly IBattleScorer _battleScorer;
        private readonly ISpeedChooser _speedChooser;
        private readonly IBestCharacterActionChoicePicker _bestCharacterActionChoicePicker;

        public IntelligentSpellUnlockChooser(IBattleEngine sim, IBattleScorer battleScorer, ISpeedChooser speedChooser, IBestCharacterActionChoicePicker bestCharacterActionChoicePicker)
        {
            _sim = sim;
            _battleScorer = battleScorer;
            _speedChooser = speedChooser;
            _bestCharacterActionChoicePicker = bestCharacterActionChoicePicker;
        }

        public List<SpellUnlockChoice> GetSpellUnlockChoices(Battle battle, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            var myTeamIndicator = aliveCharacters[0].TeamNumber == 1 ? TeamIndicator.One : TeamIndicator.Two;
            var theirTeamIndicator = aliveCharacters[0].TeamNumber == 1 ? TeamIndicator.Two : TeamIndicator.One;

            BestPickSpellChoose bestPick = new BestPickSpellChoose();

            var battleClone = battle.DeepClone();
            var myTeamSpeed = _speedChooser.GetSpeedChoices(battleClone, aliveCharacters, aliveEnemies);
            _sim.ChooseSpeed(battleClone, myTeamIndicator, myTeamSpeed);
            var theirTeamSpeed = _speedChooser.GetSpeedChoices(battleClone, aliveEnemies, aliveCharacters);
            _sim.ChooseSpeed(battleClone, aliveCharacters[0].TeamNumber == 1 ? TeamIndicator.Two : TeamIndicator.One, theirTeamSpeed);
            bool rnd = false;
            int count = 0;
            bestPick.Choices = new List<SpellUnlockChoice>();
            foreach (Character c in aliveCharacters)
            {
                bestPick.BestScore = _battleScorer.GetBattleScore(battle);
                if (c.TeamNumber == 1)
                    bestPick.BestScore = -1 * bestPick.BestScore;

                if (count == 2)
                    break;
                bestPick.Choices.Add(new SpellUnlockChoice() { CharacterId = c.Id });
                List<TalentNode> possibleList = c.TalentTreeStructure.Root.GetNextChildrenToUnlock();
                if (possibleList.Any())
                {
                    foreach (var s in possibleList)
                    {
                        var battleClone2 = battleClone.DeepClone();
                        _sim.ChooseSpellToUnlock(battleClone2, myTeamIndicator, new List<SpellUnlockChoice>()
                        {
                            new(){CharacterId = c.Id, Spell = s.Spell}
                        });
                        _sim.ChooseSpellToUnlock(battleClone2, theirTeamIndicator, new List<SpellUnlockChoice>());
                        var simChar = battleClone2.AllAliveCharacters.Single(x => x.Id == c.Id);
                        simChar.Energy = s.Spell.EnergyCost ?? 0;
                        simChar.ExtraPoint = s.Spell.MinionsCost ?? 0;
                        var choice = _bestCharacterActionChoicePicker.GetCharActionBestPick(battleClone2, simChar, aliveCharacters, aliveEnemies).Result;
                        if (choice.Spell.Name == s.Spell.Name)
                        {
                            if (choice.BestScore >= bestPick.BestScore)
                            {
                                bestPick.BestScore = choice.BestScore;
                                bestPick.Choices[count].Spell = choice.Spell;
                            }
                        }
                    }

                    if (bestPick.Choices[count].Spell == null)
                    {
                        rnd = true;
                        break;
                    }
                }

                count++;
            }

            if (rnd)
                return new RandomSpellUnlockChooser().GetSpellUnlockChoices(battle, aliveCharacters, aliveEnemies);

            return bestPick.Choices;
        }
    }
}
