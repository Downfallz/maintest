using DA.Game.Domain.Services.TalentsManagement;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.TalentsManagement.IoC
{
    public static class GameDomainDependencyInjection
    {
        public static void AddGameTalentsManagement(this IServiceCollection services)
        {
            services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddTransient<ITalentTreeManager, TalentTreeManager>();
            services.AddTransient<ICharacterTalentStatsHandler, CharacterTalentStatsHandler>();
            services.AddTransient<ICharacterDevelopmentService, CharacterDevelopmentService>();
        }
    }
}
