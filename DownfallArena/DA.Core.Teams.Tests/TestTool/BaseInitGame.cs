using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.Tests.TestTool
{
    public class BaseInitGame
    {
        static IConfigurationRoot Configuration;
        public ServiceProvider ServiceProvider { get; }

        public BaseInitGame()
        {
            var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", true, true);

            Configuration = builder.Build();
            //services.AddDbContext<GameDbContext>(item => item.UseSqlServer(Configuration.GetConnectionString("myconn")));

            var services = new ServiceCollection();

            //services.AddLogging();
            //services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            //services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            //services.AddTransient<IRoundManager, RoundManager>();
            //services.AddTransient<IBattleManager, BattleManager>();
            //services.AddTransient<IInitiativesSetter, InitiativesSetter>();
            //services.AddTransient<ICharacterTurnManager, CharacterTurnManager>();
            //services.AddTransient<IEngine, Engine>();

            ServiceProvider = services.BuildServiceProvider();
        }
    }
}
