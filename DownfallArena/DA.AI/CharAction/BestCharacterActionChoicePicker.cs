using DA.AI.CharAction.Tgt;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Threading.Tasks;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;
using DA.Game.Domain.Services;

namespace DA.AI.CharAction
{
    public class BestCharacterActionChoicePicker : IBestCharacterActionChoicePicker
    {
        private readonly IBattleEngine _simulator;
        private readonly IBattleScorer _battleScorer;
        object guard = new object();
        public BestCharacterActionChoicePicker(IBattleEngine simulator, IBattleScorer battleScorer)
        {
            _simulator = simulator;
            _battleScorer = battleScorer;
        }

        public async Task<BestPick> GetCharActionBestPick(Battle battle, Character charToPlay, List<Character> aliveCharacters, List<Character> aliveEnemies)
        {
            var possibleSpell = charToPlay.CharacterTalentStats.UnlockedSpells
                .Where(x => x.EnergyCost <= charToPlay.Energy && (!x.MinionsCost.HasValue || x.MinionsCost.Value <= charToPlay.ExtraPoint)).ToList();

            BestPick bestPick = new BestPick();
            bestPick.BestScore = _battleScorer.GetBattleScore(battle);
            if (charToPlay.TeamNumber == 1)
                bestPick.BestScore = -1 * bestPick.BestScore;
            bestPick.BestTargets = new List<Guid>();
            bestPick.Spell = possibleSpell.Single(x => x.Name == "Wait");

            List<Task> calculations = new List<Task>();
            Parallel.ForEach(possibleSpell, s =>
            {
                if (s.SpellType == SpellType.Defensive)
                {
                    int possibleTargetsCount = aliveCharacters.Count;
                    int spellTargetCount = s.NbTargets.HasValue ? s.NbTargets.Value : 0;

                    List<int> picked = new List<int>();
                    int count = 0;

                    if (spellTargetCount >= possibleTargetsCount)
                    {
                        List<Guid> targets = new List<Guid>();
                        for (int i = 0; i < possibleTargetsCount; i++)
                        {
                            targets.Add(aliveCharacters[i].Id);
                        }

                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                    }
                    else if (spellTargetCount == 1)
                    {
                        foreach (var c in aliveCharacters)
                        {
                            List<Guid> targets = new List<Guid>() { c.Id };

                            calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                        }
                    }
                    else if (spellTargetCount == 2)
                    {
                        List<Guid> targets = new List<Guid>() { aliveCharacters[0].Id, aliveCharacters[1].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));

                        targets = new List<Guid>() { aliveCharacters[1].Id, aliveCharacters[2].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));

                        targets = new List<Guid>() { aliveCharacters[0].Id, aliveCharacters[2].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                    }
                    else if (spellTargetCount == 0)
                    {
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, new List<Guid>(), bestPick));
                    }

                }
                else if (s.SpellType == SpellType.Offensive)
                {
                    int possibleTargetsCount = aliveEnemies.Count;
                    int spellTargetCount = s.NbTargets.HasValue ? s.NbTargets.Value : 0;

                    List<int> picked = new List<int>();
                    int count = 0;

                    if (spellTargetCount >= possibleTargetsCount)
                    {
                        List<Guid> targets = new List<Guid>();
                        for (int i = 0; i < possibleTargetsCount; i++)
                        {
                            targets.Add(aliveEnemies[i].Id);
                        }

                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                    }
                    else if (spellTargetCount == 1)
                    {
                        foreach (var c in aliveEnemies)
                        {
                            List<Guid> targets = new List<Guid>() { c.Id };

                            calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                        }
                    }
                    else if (spellTargetCount == 2)
                    {
                        List<Guid> targets = new List<Guid>() { aliveEnemies[0].Id, aliveEnemies[1].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));

                        targets = new List<Guid>() { aliveEnemies[1].Id, aliveEnemies[2].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));

                        targets = new List<Guid>() { aliveEnemies[0].Id, aliveEnemies[2].Id };
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, targets, bestPick));
                    }
                    else if (spellTargetCount == 0)
                    {
                        calculations.Add(BestScoreAsync(battle, charToPlay, s, new List<Guid>(), bestPick));
                    }
                }
            });

            await Task.WhenAll(calculations);

            return bestPick;
        }

        private async Task BestScoreAsync(Battle battle, Character charToPlay, Spell s, List<Guid> targets, BestPick pick)
        {
            var battleClone = battle.Clone();

            _simulator.PlayAndResolveCharacterAction(battleClone, new CharacterActionChoice()
            {
                CharacterId = charToPlay.Id,
                Spell = s,
                Targets = targets
            });

            var score = _battleScorer.GetBattleScore(battleClone);

            if (charToPlay.TeamNumber == 1)
                score = -1 * score;

            lock (guard)
            {
                if (score >= pick.BestScore)
                {
                    pick.BestScore = score;
                    pick.BestTargets = targets;
                    pick.Spell = s;
                }
            }
        }
    }
}
