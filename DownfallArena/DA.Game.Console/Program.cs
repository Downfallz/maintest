using DA.AI;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.Game;
using DA.Game.CombatMechanic.IoC;
using DA.Game.Domain.Services;
using DA.Game.IoC;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using Microsoft.Extensions.DependencyInjection;
using System;
using DA.AI.CharAction;

namespace DA.Csl
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceCollection services = new ServiceCollection();

            services.AddLogging();
            services.AddGameTalentsManagement();
            services.AddGameCombatMechanic();
            services.AddGameResources();
            services.AddGame();

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            BattleEngine battleEngine = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
            BattleEngine sim = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
            var moreIntelligentPlayerHandler = new BaseAIPlayerHandler(battleEngine,
                new RandomSpeedChooser(),
                new IntelligentSpellUnlockChooser(sim, new BetterBattleScorer(), new RandomSpeedChooser(), new BestCharacterActionChoicePicker(sim, new BetterBattleScorer())),
                new IntelligentCharacterActionChooser(new BestCharacterActionChoicePicker(sim, new BetterBattleScorer())));
            DAGame test = new DAGame(battleEngine);

            test.Start(new CslUiPlayerHandler(battleEngine), moreIntelligentPlayerHandler);

            //IBattleEngine simulator = (IBattleEngine)serviceProvider.GetService<IBattleEngine>();
            //DAGame test = new DAGame(battleEngine);
            //SuperAIPlayerHandler randomAi = new SuperAIPlayerHandler(battleEngine, simulator,
            //    new SpeedChooser(),
            //    new SpellUnlockChooser(), new RandomTargetChooser());
            //CslUiPlayerHandler playerHandler = new CslUiPlayerHandler(battleEngine);

            //test.Start(playerHandler, randomAi);
            ////Console.WriteLine(jokeProvider.GetJoke());
            //Console.ReadLine();
        }
    }
}
