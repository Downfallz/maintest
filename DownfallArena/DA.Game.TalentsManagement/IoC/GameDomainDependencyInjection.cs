using DA.Game.Domain.Services.GameFlowEngine.TalentsManagement;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.TalentsManagement.IoC
{
    public static class GameDomainDependencyInjection
    {
        public static void AddGameTalentsManagement(this IServiceCollection services)
        {
            services.AddScoped<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddScoped<ITalentTreeManager, TalentTreeManager>();
            services.AddScoped<ICharacterTalentStatsHandler, CharacterTalentStatsHandler>();
            services.AddScoped<ICharacterDevelopmentService, CharacterDevelopmentService>();
        }
    }
}
