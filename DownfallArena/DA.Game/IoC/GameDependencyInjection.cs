using DA.Game.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.IoC
{
    public static class GameDependencyInjection
    {
        public static void AddGame(this IServiceCollection services)
        {
            services.AddTransient<ITeamService, TeamService>();
            services.AddTransient<IRoundService, RoundService>();
            services.AddTransient<IBattleEngine, BattleEngine>();
        }
    }
}
