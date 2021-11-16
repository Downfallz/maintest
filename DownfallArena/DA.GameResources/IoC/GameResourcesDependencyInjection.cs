using DA.Game.Resources.Generator;
using DA.Game.Resources.Spells;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DA.Game.Resources.IoC
{
    public static class GameResourcesDependencyInjection
    {
        public static void AddGameResources(this IServiceCollection services)
        {
            services.AddSingleton<IAssassinSpells>(new JsonAssassinSpells("AssassinSpells"));
            services.AddSingleton<IBerserkerSpells>(new JsonBerserkerSpells("BerserkerSpells"));
            services.AddSingleton<IBrawlerSpells>(new JsonBrawlerSpells("BrawlerSpells"));
            services.AddSingleton<ICreatureSpells>(new JsonCreatureSpells("CreatureSpells"));
            services.AddSingleton<ILeechSpells>(new JsonLeechSpells("LeechSpells"));
            services.AddSingleton<IMercenarySpells>(new JsonMercenarySpells("MercenarySpells"));
            services.AddSingleton<INecromancerSpells>(new JsonNecromancerSpells("NecromancerSpells"));
            services.AddSingleton<IScoundrelSpells>(new JsonScoundrelSpells("ScoundrelSpells"));
            services.AddSingleton<IShamanSpells>(new JsonShamanSpells("ShamanSpells"));
            services.AddSingleton<ISorcererSpells>(new JsonSorcererSpells("SorcererSpells"));
            services.AddSingleton<ITricksterSpells>(new JsonTricksterSpells("TricksterSpells"));
            services.AddSingleton<IWarlordSpells>(new JsonWarlordSpells("WarlordSpells"));
            services.AddSingleton<IWizardSpells>(new JsonWizardSpells("WizardSpells"));
            services.AddSingleton<IResourceContext, ResourceContext>();
            services.AddScoped<IGetSpell, GetSpell>();
        }
    }
}
