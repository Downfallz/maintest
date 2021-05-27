using DA.Game.Resources.Generator;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.Resources.IoC
{
    public static class GameResourcesDependencyInjection
    {
        public static void AddGameResources(this IServiceCollection services)
        {
            services.AddScoped<IGetSpell, GetSpell>();
        }
    }
}
