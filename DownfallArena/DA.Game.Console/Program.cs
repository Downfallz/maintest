using System;
using DA.AI;
using DA.AI.Spd;
using DA.AI.Spl;
using DA.AI.Tgt;
using DA.Game;
using DA.Game.CombatMechanic.IoC;
using DA.Game.Domain.Services;
using DA.Game.IoC;
using DA.Game.Resources.Generator;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Csl
{
    class Program
    {
        static void Main(string[] args)
        {

            //.AddSingleton<IDatabaseConnection, SqlConnection>()
            //.AddTransient<IJokeProvider, JavaJokeProvider>();
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddGameTalentsManagement();
            services.AddGameCombatMechanic();
            services.AddGameResources();
            services.AddGame();

            var serviceProvider = services.BuildServiceProvider();

            //var jokeProvider = serviceProvider.GetService<IJokeProvider>();
            var battleEngine = (BattleEngine)serviceProvider.GetService<IBattleEngine>();
            var simulator = (IBattleEngine)serviceProvider.GetService<IBattleEngine>();
            var test = new DAGame(battleEngine);
            var randomAi = new SuperAIPlayerHandler(battleEngine, simulator, 
                new SpeedChooser(), 
                new SpellChooser(), new RandomTargetChooser());
            var playerHandler = new CslUiPlayerHandler(battleEngine);

            test.Start(playerHandler, randomAi);
            //Console.WriteLine(jokeProvider.GetJoke());
            Console.ReadLine();
        }
    }
}
