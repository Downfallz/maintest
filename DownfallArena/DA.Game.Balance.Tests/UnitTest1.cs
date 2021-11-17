using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DA.AI;
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

            
            //SuperAIPlayerHandler randomAi = new SuperAIPlayerHandler(battleEngine, simulator,
            //    new SpeedChooser(),
            //    new SpellUnlockChooser(), new RandomTargetChooser());

            //var aa = new RandomAIPlayerHandler(battleEngine, simulator, new RandomSpeedChooser(),
            //    new RandomSpellUnlockChooser(), new RandomTargetChooser());

            ConcurrentBag<BattleResult> winningResults = new ConcurrentBag<BattleResult>();

            Parallel.For(0, 3000, (i) =>
            {
                BattleEngine battleEngine = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
                DAGame test = new DAGame(battleEngine);
                test.Start(new RandomAIPlayerHandler(battleEngine), new RandomAIPlayerHandler(battleEngine));

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

            var allTop3 = winningResults.SelectMany(x => x.Top5Spell).ToList();
            
            Dictionary<string, Statstest> dic = new Dictionary<string, Statstest>();

            foreach (var t in allTop3)
            {
                if (dic.ContainsKey(t.Item1.Name))
                {
                    dic[t.Item1.Name].NbOccurence++;
                    dic[t.Item1.Name].OccurenceNbCast.Add(t.Item2);
                }
                else
                {
                    dic.Add(t.Item1.Name, new Statstest(){Spell = t.Item1, NbOccurence = 1, OccurenceNbCast = new List<int>(){t.Item2}});
                }
            }

            var ordered = dic.Values.Where(x => x.Spell.Level == 3).OrderByDescending(x => x.NbOccurence).ToList();
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