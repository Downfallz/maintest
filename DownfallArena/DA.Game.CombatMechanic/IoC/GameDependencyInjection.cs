using DA.Game.CombatMechanic.Tools;
using DA.Game.Domain.Services.GameFlowEngine.CombatMechanic;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.CombatMechanic.IoC
{
    public static class GameDependencyInjection
    {
        public static void AddGameCombatMechanic(this IServiceCollection services)
        {
            services.AddScoped<IAppliedEffectService, AppliedEffectService>();
            services.AddScoped<ICharacterCondService, CharacterCondService>();
            services.AddScoped<ISpellResolverService, SpellResolverService>();
            services.AddScoped<IStatModifierApplyer, StatModifierApplyer>();
        }
    }
}
