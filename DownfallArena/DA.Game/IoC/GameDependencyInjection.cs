using DA.Game.Domain.Services;
using DA.Game.Domain.Services.GameFlowEngine;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.IoC
{
    public static class GameDependencyInjection
    {
        public static void AddGame(this IServiceCollection services)
        {
            services.AddScoped<ITeamService, TeamService>();
            services.AddScoped<IRoundService, RoundService>();
            services.AddScoped<IBattleEngine, BattleEngine>();
        }
    }
}
