using DA.Game.Domain.Services.GameFlowEngine.TalentsManagement;
using DA.Game.Resources.Generator;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.IntegrationTests
{
    public class CharacterDevelopmentServiceIntgTests
    {

        public ServiceProvider ServiceProvider { get; }

        public CharacterDevelopmentServiceIntgTests()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddScoped<IGetSpell, GetSpell>();
            services.AddScoped<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddScoped<ITalentTreeManager, TalentTreeManager>();
            services.AddScoped<ICharacterTalentStatsHandler, CharacterTalentStatsHandler>();
            services.AddScoped<ICharacterDevelopmentService, CharacterDevelopmentService>();

            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Test1()
        {
            ICharacterDevelopmentService sut = (ICharacterDevelopmentService)ServiceProvider.GetService(typeof(ICharacterDevelopmentService));

            Domain.Models.GameFlowEngine.Character newChar = sut.InitializeNewCharacter();

            sut.UnlockSpell(newChar, newChar.CharacterTalentStats.UnlockableSpells[0]);
            Assert.NotNull(newChar);
        }
    }
}
