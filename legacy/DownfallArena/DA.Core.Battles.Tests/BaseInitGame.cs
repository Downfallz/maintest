using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.Tests
{
    public class BaseInitGame
    {

        public ServiceProvider ServiceProvider { get; }

        public BaseInitGame()
        {

            //services.AddDbContext<GameDbContext>(item => item.UseSqlServer(Configuration.GetConnectionString("myconn")));

            var services = new ServiceCollection();

            services.AddLogging();
            services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            //services.AddTransient<IRoundManager, RoundManager>();
            //services.AddTransient<IBattleManager, BattleManager>();
            //services.AddTransient<IInitiativesSetter, InitiativesSetter>();
            //services.AddTransient<ICharacterTurnManager, CharacterTurnManager>();
            //services.AddTransient<IEngine, Engine>();

            ServiceProvider = services.BuildServiceProvider();
        }
    }
}
