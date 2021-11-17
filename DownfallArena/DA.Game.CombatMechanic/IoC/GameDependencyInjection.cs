using DA.Game.CombatMechanic.Tools;
using DA.Game.Domain.Services.CombatMechanic;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.CombatMechanic.IoC
{
    public static class GameDependencyInjection
    {
        public static void AddGameCombatMechanic(this IServiceCollection services)
        {
            services.AddTransient<IAppliedEffectService, AppliedEffectService>();
            services.AddTransient<ICharacterCondService, CharacterCondService>();
            services.AddTransient<ISpellResolverService, SpellResolverService>();
            services.AddTransient<IStatModifierApplyer, StatModifierApplyer>();
        }
    }
}
