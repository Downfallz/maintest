using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.Resources.Generator;
using DA.Game.Resources.IoC;
using DA.Game.Resources.Spells;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace DA.Game.Resources.Tests
{
    public class IntegratedTests
    {
        [Fact]
        public void unittest()
        {

            //Configuration = builder.Build();
            //services.AddDbContext<GameDbContext>(item => item.UseSqlServer(Configuration.GetConnectionString("myconn")));

            ServiceCollection services = new ServiceCollection();

            //services.AddLogging();
            //services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            //services.AddTransient<ITalentTreeBuilder, TalentTreeBuilder>();
            //services.AddTransient<IRoundManager, RoundManager>();
            //services.AddTransient<IBattleManager, BattleManager>();
            //services.AddTransient<IInitiativesSetter, InitiativesSetter>();
            //services.AddTransient<ICharacterTurnManager, CharacterTurnManager>();
            //services.AddTransient<IEngine, Engine>();
            services.AddGameResources();
            var serviceProvider = services.BuildServiceProvider();


            var getSpell = serviceProvider.GetService<IGetSpell>();
            var eng = getSpell.FromEnum(TalentList.EngulfingFlames);


        }

    }
}