using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Game;
using DA.Game.CombatMechanic;
using DA.Game.Domain;
using DA.Game.Domain.Services;
using DA.Game.Domain.Services.CombatMechanic;
using DA.Game.Domain.Services.TalentsManagement;
using Microsoft.Extensions.DependencyInjection;

namespace DA.AI
{
    public class SimFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SimFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public IBattleEngine CreateBattleEngineSimulator()
        {
            var teamService = _serviceProvider.GetService<ITeamService>();
            var appliedEffectService = _serviceProvider.GetService<IAppliedEffectService>();
            var charCondService = _serviceProvider.GetService<ICharacterCondService>();
            var characterDevelopmentService  = _serviceProvider.GetService<ICharacterDevelopmentService>();
            var gameLogger = _serviceProvider.GetService<IGameLogger>();

            var spellResolverService = new SpellResolverService(appliedEffectService);//, gameLogger);

            var roundService = new RoundService(appliedEffectService, charCondService, characterDevelopmentService,
                spellResolverService);

            return new BattleEngine(teamService, roundService);
        }
    }
}
