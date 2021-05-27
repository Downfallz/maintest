using DA.Core.Domain.Base.Teams;
using DA.Core.Teams.Abstractions;

namespace DA.Core.Teams
{
    public class TeamService : ITeamService
    {
        private readonly ICharacterDevelopmentService _characterService;

        public TeamService(ICharacterDevelopmentService characterService)
        {
            _characterService = characterService;
        }

        public Team InitializeNewTeam()
        {
            var team = new Team();
            var char1 = _characterService.InitializeNewCharacter();
            char1.Name = "Creature 1";
            var char2 = _characterService.InitializeNewCharacter();
            char2.Name = "Creature 2";
            var char3 = _characterService.InitializeNewCharacter();
            char3.Name = "Creature 3";

            team.Characters.Add(char1);
            team.Characters.Add(char2);
            team.Characters.Add(char3);

            return team;
        }
    }
}