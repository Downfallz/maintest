using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.Tests
{
    public class BattleServiceIntgTests
    {
        public ServiceProvider ServiceProvider { get; }
        static IBattleEngine sut;

        public BattleServiceIntgTests()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddScoped<IGetSpell, GetSpell>();
            services.AddScoped<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddScoped<ITalentTreeManager, TalentTreeManager>();
            services.AddScoped<ICharacterTalentStatsHandler, CharacterTalentStatsHandler>();
            services.AddScoped<ICharacterDevelopmentService, CharacterDevelopmentService>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IBattleEngine, BattleEngine>();
            services.AddScoped<IAppliedEffectManager, AppliedEffectManager>();
            services.AddScoped<ITeamService, TeamService>();
            services.AddTransient<IBattleEngine, BattleEngine>();
            services.AddScoped<ICharacterCondManager, CharacterCondManager>();
            services.AddScoped<IRoundService, RoundService>();
            services.AddScoped<IPlayerActionHandler, PlayerActionHandler>();
            services.AddScoped<IStatModifierApplyer, StatModifierApplyer>();
            services.AddScoped<IDAGame, DAGame>();
            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Test1()
        {
            sut = (IBattleEngine)ServiceProvider.GetService(typeof(IBattleEngine));

            var sim = (IBattleEngine)ServiceProvider.GetService(typeof(IBattleEngine));
            var game = new DAGame(sut);
            
            game.Start(new RandomAIPlayerHandler(sut, sim), new RandomAIPlayerHandler(sut, sim));

        }

   
    }

}
