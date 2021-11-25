using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DA.AI;
using DA.AI.CharAction;
using DA.AI.CharAction.Tgt;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game.CombatMechanic.IoC;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Domain.Services;
using DA.Game.IoC;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.Balance.Tests
{
    public class UnitTest1
    {

        public UnitTest1()
        {
            //ServiceCollection services = new ServiceCollection();

            //services.AddGameTalentsManagement();
            //services.AddGameCombatMechanic();
            //services.AddGameResources();
            //services.AddGame();

            //ServiceProvider serviceProvider = services.BuildServiceProvider();

            //BattleEngine battleEngine = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
            //IBattleEngine simulator = (IBattleEngine)serviceProvider.GetService<IBattleEngine>();

            //DAGame test = new DAGame(battleEngine);
            ////SuperAIPlayerHandler randomAi = new SuperAIPlayerHandler(battleEngine, simulator,
            ////    new SpeedChooser(),
            ////    new SpellUnlockChooser(), new RandomTargetChooser());

            ////var aa = new RandomAIPlayerHandler(battleEngine, simulator, new RandomSpeedChooser(),
            ////    new RandomSpellUnlockChooser(), new RandomTargetChooser());

            //test.Start(new RandomAIPlayerHandler(battleEngine), new RandomAIPlayerHandler(battleEngine));
        }

        [Fact]
        public void Test1()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddGameTalentsManagement();
            services.AddGameCombatMechanic();
            services.AddGameResources();
            services.AddGame();

            ServiceProvider serviceProvider = services.BuildServiceProvider();


            IBattleEngine simulator = (IBattleEngine)serviceProvider.GetService<IBattleEngine>();

            ConcurrentBag<BattleResult> winningResults = new ConcurrentBag<BattleResult>();

            Parallel.For(0, 5, (i) =>
            {
                BattleEngine battleEngine = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
                BattleEngine sim = (BattleEngine)serviceProvider.GetService<IBattleEngine>();

                var rndPlayerHandler = new RandomAIPlayerHandler(battleEngine);
                var slightlyBetterRndPlayerHandler = new BaseAIPlayerHandler(battleEngine,
                    new RandomSpeedChooser(),
                    new RandomSpellUnlockChooser(),
                    new BasicCharacterActionChooser(new BetterRandomSpellChooser(), new RandomTargetChooser()));
                var intelligentPlayerHandler = new BaseAIPlayerHandler(battleEngine,
                    new RandomSpeedChooser(),
                    new RandomSpellUnlockChooser(),
                    new IntelligentCharacterActionChooser(new BestCharacterActionChoicePicker(sim, new BasicBattleScorer())));
                var moreIntelligentPlayerHandler = new BaseAIPlayerHandler(battleEngine,
                    new RandomSpeedChooser(),
                    new IntelligentSpellUnlockChooser(sim, new BetterBattleScorer(), new RandomSpeedChooser(), new BestCharacterActionChoicePicker(sim, new BetterBattleScorer())),
                    new IntelligentCharacterActionChooser(new BestCharacterActionChoicePicker(sim, new BetterBattleScorer())));

                DAGame test = new DAGame(battleEngine);

                test.Start(intelligentPlayerHandler, slightlyBetterRndPlayerHandler);

                Team winningTeam = test.Battle.Winner == 1 ? test.Battle.TeamOne : test.Battle.TeamTwo;

                var titest = test.Battle.FinishedRoundsHistory.SelectMany(x => x.CharacterActionChoices).Where(x => winningTeam.Characters.Any(z => z.Id == x.CharacterId))
                    .GroupBy(g => g.Spell).Select(x => new Tuple<Spell, int>(x.Key, x.Count()));

                BattleResult result = new BattleResult();
                result.Top5Spell = titest.OrderByDescending(x => x.Item2).Take(5).ToList();
                result.WinningTeamNo = test.Battle.Winner;
                result.OneTeamDead = test.Battle.OneTeamIsDead;

                winningResults.Add(result);
            });

            var countTeamDead = winningResults.Count(x => x.OneTeamDead);
            var countRoundsEnded = winningResults.Count(x => !x.OneTeamDead);
            var totalWinTeamOne = winningResults.Count(x => x.WinningTeamNo == 1);
            var totalWinTeamtwo = winningResults.Count(x => x.WinningTeamNo == 2);

            var teamOneallTop3 = winningResults.Where(x => x.WinningTeamNo == 1).SelectMany(x => x.Top5Spell).ToList();
            var teamTwoallTop3 = winningResults.Where(x => x.WinningTeamNo == 2).SelectMany(x => x.Top5Spell).ToList();
            
            Dictionary<string, Statstest> dicOne = new Dictionary<string, Statstest>();

            foreach (var t in teamOneallTop3)
            {
                if (dicOne.ContainsKey(t.Item1.Name))
                {
                    dicOne[t.Item1.Name].NbOccurence++;
                    dicOne[t.Item1.Name].OccurenceNbCast.Add(t.Item2);
                }
                else
                {
                    dicOne.Add(t.Item1.Name, new Statstest(){Spell = t.Item1, NbOccurence = 1, OccurenceNbCast = new List<int>(){t.Item2}});
                }
            }
            var orderedLevelOneOne = dicOne.Values.Where(x => x.Spell.Level == 1).OrderByDescending(x => x.NbOccurence).ToList();
            var orderedLevelTwoOne = dicOne.Values.Where(x => x.Spell.Level == 2).OrderByDescending(x => x.NbOccurence).ToList();
            var orderedOne = dicOne.Values.Where(x => x.Spell.Level == 3).OrderByDescending(x => x.NbOccurence).ToList();

            Dictionary<string, Statstest> dicTwo = new Dictionary<string, Statstest>();

            foreach (var t in teamTwoallTop3)
            {
                if (dicTwo.ContainsKey(t.Item1.Name))
                {
                    dicTwo[t.Item1.Name].NbOccurence++;
                    dicTwo[t.Item1.Name].OccurenceNbCast.Add(t.Item2);
                }
                else
                {
                    dicTwo.Add(t.Item1.Name, new Statstest() { Spell = t.Item1, NbOccurence = 1, OccurenceNbCast = new List<int>() { t.Item2 } });
                }
            }
            var orderedLevelOneTwo = dicTwo.Values.Where(x => x.Spell.Level == 1).OrderByDescending(x => x.NbOccurence).ToList();
            var orderedLevelTwoTwo = dicTwo.Values.Where(x => x.Spell.Level == 2).OrderByDescending(x => x.NbOccurence).ToList();
            var orderedTwo = dicTwo.Values.Where(x => x.Spell.Level == 3).OrderByDescending(x => x.NbOccurence).ToList();
        }
    }

    class Statstest
    {
        public Statstest()
        {
            OccurenceNbCast = new List<int>();
        }

        public Spell Spell { get; set; }
        public int NbOccurence { get; set; }
        public List<int> OccurenceNbCast { get; set; }
        public int AvgTotalCast => OccurenceNbCast.Sum() / NbOccurence;
    }
}